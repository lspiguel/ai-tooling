using System;
using System.Collections.Generic;
using System.Linq;

using D365ContextExporter.Helpers;
using D365ContextExporter.Models;

using Microsoft.Xrm.Sdk;

using NUnit.Framework;

namespace D365ContextExporter.Tests.HelpersTests
{
    [TestFixture]
    public class EntityJsonSerializerTests
    {
        private static OutputSettings NoFilter() => new OutputSettings { AttributeDenyList = new List<string>() };
        private static OutputSettings WithDenyList(params string[] items) =>
            new OutputSettings { AttributeDenyList = new List<string>(items) };

        private static Entity MakeEntity(string logicalName, Guid id, params (string key, object? value)[] attrs)
        {
            var e = new Entity(logicalName, id);
            foreach (var (k, v) in attrs)
                e.Attributes[k] = v;
            return e;
        }

        [Test]
        public void SerializeEntity_AddsIdKey()
        {
            var id = Guid.NewGuid();
            var entity = MakeEntity("account", id);
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["_id"], Is.EqualTo(id.ToString()));
        }

        [Test]
        public void SerializeEntity_NullAttribute_MapsToNull()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("name", null));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["name"], Is.Null);
        }

        [Test]
        public void SerializeEntity_StringPassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("name", (object?)"Contoso"));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["name"], Is.EqualTo("Contoso"));
        }

        [Test]
        public void SerializeEntity_IntPassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("employees", (object?)42));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["employees"], Is.EqualTo(42));
        }

        [Test]
        public void SerializeEntity_BoolPassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("active", (object?)true));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["active"], Is.EqualTo(true));
        }

        [Test]
        public void SerializeEntity_GuidConvertedToString()
        {
            var g = Guid.NewGuid();
            var entity = MakeEntity("account", Guid.NewGuid(), ("ref_id", (object?)g));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["ref_id"], Is.EqualTo(g.ToString()));
        }

        [Test]
        public void SerializeEntity_DateTimeConvertedToIso8601()
        {
            var dt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var entity = MakeEntity("account", Guid.NewGuid(), ("createdon", (object?)dt));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["createdon"], Is.EqualTo("2026-01-15T10:30:00Z"));
        }

        [Test]
        public void SerializeEntity_EntityReference_ConvertedToDict()
        {
            var refId = Guid.NewGuid();
            var er = new EntityReference("contact", refId) { Name = "John" };
            var entity = MakeEntity("account", Guid.NewGuid(), ("primarycontactid", (object?)er));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            var refDict = (Dictionary<string, object?>)dict["primarycontactid"]!;
            Assert.That(refDict["id"], Is.EqualTo(refId.ToString()));
            Assert.That(refDict["logicalName"], Is.EqualTo("contact"));
            Assert.That(refDict["name"], Is.EqualTo("John"));
        }

        [Test]
        public void SerializeEntity_OptionSetValue_ConvertedToInt()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("statuscode", (object?)new OptionSetValue(1)));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["statuscode"], Is.EqualTo(1));
        }

        [Test]
        public void SerializeEntity_OptionSetValueCollection_ConvertedToIntList()
        {
            var col = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) };
            var entity = MakeEntity("account", Guid.NewGuid(), ("categories", (object?)col));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            var list = (List<int>)dict["categories"]!;
            Assert.That(list, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void SerializeEntity_Money_ConvertedToDecimal()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("revenue", (object?)new Money(1234.56m)));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["revenue"], Is.EqualTo(1234.56m));
        }

        [Test]
        public void SerializeEntity_AliasedValue_Unwrapped()
        {
            var av = new AliasedValue("contact", "fullname", "Jane Doe");
            var entity = MakeEntity("account", Guid.NewGuid(), ("contact1.fullname", (object?)av));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["contact1.fullname"], Is.EqualTo("Jane Doe"));
        }

        [Test]
        public void SerializeEntity_AliasedValueWrappingEntityReference_UnwrappedToDict()
        {
            var refId = Guid.NewGuid();
            var er = new EntityReference("systemuser", refId);
            var av = new AliasedValue("contact", "ownerid", er);
            var entity = MakeEntity("account", Guid.NewGuid(), ("a.ownerid", (object?)av));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            var refDict = (Dictionary<string, object?>)dict["a.ownerid"]!;
            Assert.That(refDict["id"], Is.EqualTo(refId.ToString()));
        }

        [Test]
        public void SerializeEntity_DenyList_FiltersMatchingAttributes()
        {
            var entity = MakeEntity("account", Guid.NewGuid(),
                ("name", (object?)"Contoso"),
                ("apikey", (object?)"secret123"),
                ("tokenvalue", (object?)"abc"));
            var dict = EntityJsonSerializer.SerializeEntity(entity, new[] { "key", "token" });
            Assert.That(dict.ContainsKey("name"), Is.True);
            Assert.That(dict.ContainsKey("apikey"), Is.False);
            Assert.That(dict.ContainsKey("tokenvalue"), Is.False);
        }

        [Test]
        public void SerializeEntities_UsesOutputSettingsDenyList()
        {
            var settings = WithDenyList("password");
            var entity = MakeEntity("account", Guid.NewGuid(),
                ("name", (object?)"Acme"),
                ("adminpassword", (object?)"hunter2"));
            var results = EntityJsonSerializer.SerializeEntities(new[] { entity }, settings);
            Assert.That(results[0].ContainsKey("adminpassword"), Is.False);
            Assert.That(results[0].ContainsKey("name"), Is.True);
        }

        [Test]
        public void SerializeEntity_EntityCollection_RecursesIntoChildren()
        {
            var child = new Entity("task", Guid.NewGuid());
            child.Attributes["subject"] = "Call back";
            var col = new EntityCollection(new List<Entity> { child });
            var entity = MakeEntity("account", Guid.NewGuid(), ("tasks", (object?)col));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            var children = (List<Dictionary<string, object?>>)dict["tasks"]!;
            Assert.That(children.Count, Is.EqualTo(1));
            Assert.That(children[0]["subject"], Is.EqualTo("Call back"));
        }

        [Test]
        public void SerializeEntity_LongPassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("bigcount", (object?)(long)9_000_000_000L));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["bigcount"], Is.EqualTo(9_000_000_000L));
        }

        [Test]
        public void SerializeEntity_DoublePassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("ratio", (object?)3.14));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["ratio"], Is.EqualTo(3.14));
        }

        [Test]
        public void SerializeEntity_DecimalPassthrough()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("amount", (object?)99.99m));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["amount"], Is.EqualTo(99.99m));
        }

        [Test]
        public void SerializeEntity_UnknownType_FallsBackToToString()
        {
            var entity = MakeEntity("account", Guid.NewGuid(), ("custom", (object?)new Uri("https://example.com")));
            var dict = EntityJsonSerializer.SerializeEntity(entity, Array.Empty<string>());
            Assert.That(dict["custom"], Is.EqualTo("https://example.com/"));
        }

        [Test]
        public void SerializeEntities_MultipleEntities_ReturnsCorrectCount()
        {
            var entities = new[]
            {
                MakeEntity("account", Guid.NewGuid(), ("name", (object?)"A")),
                MakeEntity("account", Guid.NewGuid(), ("name", (object?)"B")),
                MakeEntity("account", Guid.NewGuid(), ("name", (object?)"C")),
            };
            var results = EntityJsonSerializer.SerializeEntities(entities, NoFilter());
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results[0]["name"], Is.EqualTo("A"));
            Assert.That(results[2]["name"], Is.EqualTo("C"));
        }

        [Test]
        public void SerializeEntities_EmptyList_ReturnsEmptyList()
        {
            var results = EntityJsonSerializer.SerializeEntities(Array.Empty<Entity>(), NoFilter());
            Assert.That(results, Is.Empty);
        }
    }
}
