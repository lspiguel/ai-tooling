using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Lspiguel.Xrm.D365AgentCompanionMcp.Paging;
using Lspiguel.Xrm.D365AgentCompanionMcp.Tests.Fakes;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tests.PagingTests
{
    /// <summary>Pages within the response cap, and cursors bound to connection and tool with expiry (SPEC §6.2).</summary>
    [TestFixture]
    public class PagerTests
    {
        private static readonly Func<JArray, string?, JObject> Build = (items, cursor) => new JObject { ["items"] = items, ["nextCursor"] = cursor };

        [Test]
        public async Task Page_EndsAtPageSize_AndContinues()
        {
            var pager = new Pager(new ListSource(Items(5), batch: 2), pageSize: 3);

            var first = await pager.NextPageAsync(100000, Build);
            Assert.That(Ids(first), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(pager.HasMore, Is.True);

            var second = await pager.NextPageAsync(100000, Build);
            Assert.That(Ids(second), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(pager.HasMore, Is.False);
        }

        [Test]
        public async Task Page_EndsEarlyAtAWholeItemUnderTheCap()
        {
            var items = Items(10, padding: 100);
            var cap = 450;
            var pager = new Pager(new ListSource(items, batch: 10), pageSize: 10);

            var first = await pager.NextPageAsync(cap, Build);

            Assert.That(first.Count, Is.InRange(1, 9));
            Assert.That(Build(first, new string('0', CursorStore.CursorLength)).ToString(Formatting.None).Length, Is.LessThanOrEqualTo(cap));
            var rest = new List<int>();
            while (pager.HasMore)
            {
                rest.AddRange(Ids(await pager.NextPageAsync(cap, Build)));
            }

            Assert.That(Ids(first).Concat(rest), Is.EqualTo(Enumerable.Range(0, 10)), "no item lost or repeated");
        }

        [Test]
        public async Task Page_SingleItemLargerThanTheCap_IsStillReturnedWhole()
        {
            var pager = new Pager(new ListSource(Items(2, padding: 5000), batch: 2), pageSize: 2);

            var first = await pager.NextPageAsync(1000, Build);

            Assert.That(first.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task LastPage_SaysSoInsteadOfLeadingToAnEmptyPage()
        {
            var pager = new Pager(new ListSource(Items(4), batch: 2), pageSize: 2);

            await pager.NextPageAsync(100000, Build);
            await pager.NextPageAsync(100000, Build);

            Assert.That(pager.HasMore, Is.False);
        }

        [Test]
        public void Cursor_IsBoundToConnectionAndTool()
        {
            var cursors = new CursorStore(new FakeClock());
            var pager = new Pager(new ListSource(Items(1), 1), 1);
            var cursor = cursors.Open("conn-a", "list_publishers", pager);

            Assert.That(cursor.Length, Is.EqualTo(CursorStore.CursorLength));
            Assert.That(cursors.Find(cursor, "conn-a", "list_publishers"), Is.SameAs(pager));
            Assert.That(cursors.Find(cursor, "conn-b", "list_publishers"), Is.Null);
            Assert.That(cursors.Find(cursor, "conn-a", "list_solutions"), Is.Null);
            Assert.That(cursors.Find("unknown", "conn-a", "list_publishers"), Is.Null);
        }

        [Test]
        public void Cursor_ExpiresAfterTenMinutesUnused_UseKeepsItAlive()
        {
            var clock = new FakeClock();
            var cursors = new CursorStore(clock);
            var cursor = cursors.Open("conn-a", "list_publishers", new Pager(new ListSource(Items(1), 1), 1));

            clock.Advance(TimeSpan.FromMinutes(9));
            Assert.That(cursors.Find(cursor, "conn-a", "list_publishers"), Is.Not.Null);

            clock.Advance(TimeSpan.FromMinutes(9));
            Assert.That(cursors.Find(cursor, "conn-a", "list_publishers"), Is.Not.Null, "used nine minutes ago");

            clock.Advance(TimeSpan.FromMinutes(11));
            Assert.That(cursors.Find(cursor, "conn-a", "list_publishers"), Is.Null);
        }

        [Test]
        public void Cursor_ReleasedWithItsConnection()
        {
            var cursors = new CursorStore(new FakeClock());
            var a = cursors.Open("conn-a", "list_publishers", new Pager(new ListSource(Items(1), 1), 1));
            var b = cursors.Open("conn-b", "list_publishers", new Pager(new ListSource(Items(1), 1), 1));

            cursors.ReleaseConnection("conn-a");

            Assert.That(cursors.Find(a, "conn-a", "list_publishers"), Is.Null);
            Assert.That(cursors.Find(b, "conn-b", "list_publishers"), Is.Not.Null);
        }

        private static List<JObject> Items(int count, int padding = 0) =>
            Enumerable.Range(0, count).Select(i => new JObject { ["id"] = i, ["text"] = new string('x', padding) }).ToList();

        private static IEnumerable<int> Ids(JArray items) => items.Select(i => (int)i["id"]!);

        /// <summary>Hands out a fixed list in batches.</summary>
        private sealed class ListSource : IPageSource
        {
            private readonly Queue<JObject> _items;
            private readonly int _batch;

            public ListSource(IEnumerable<JObject> items, int batch)
            {
                _items = new Queue<JObject>(items);
                _batch = batch;
            }

            public bool Exhausted => _items.Count == 0;

            public Task<IReadOnlyList<JObject>> FetchAsync(int batchSize)
            {
                var batch = new List<JObject>();
                while (batch.Count < _batch && _items.Count > 0)
                {
                    batch.Add(_items.Dequeue());
                }

                return Task.FromResult<IReadOnlyList<JObject>>(batch);
            }
        }
    }
}
