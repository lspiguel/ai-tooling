using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using D365ContextExporter.Models;
using D365ContextExporter.Queries;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

using Moq;

using NUnit.Framework;

namespace D365ContextExporter.Tests.QueriesTests
{
    [TestFixture]
    public class MetadataQueryRunnerTests
    {
        private static QueryDefinition MakeQuery(string target) =>
            new QueryDefinition { Id = "meta-query", Type = "metadata", MetadataTarget = target, ResultKey = "result" };

        private static Mock<IOrganizationService> MockWithEntitiesResponse(EntityFilters filter, EntityMetadata[] metadata)
        {
            var mock = new Mock<IOrganizationService>();
            var response = new RetrieveAllEntitiesResponse();
            response.Results["EntityMetadata"] = metadata;
            mock.Setup(s => s.Execute(It.Is<RetrieveAllEntitiesRequest>(r => r.EntityFilters == filter)))
                .Returns(response);
            return mock;
        }

        [Test]
        public void Run_Entities_ReturnsMappedList()
        {
            var em = new EntityMetadata { LogicalName = "account" };
            var mock = MockWithEntitiesResponse(EntityFilters.Entity, new[] { em });
            var runner = new MetadataQueryRunner(mock.Object, _ => { });

            var result = runner.Run(MakeQuery("entities"), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]["logicalName"], Is.EqualTo("account"));
        }

        [Test]
        public void Run_Attributes_ReturnsFlatList()
        {
            var attr = new StringAttributeMetadata { LogicalName = "name" };
            var em = new EntityMetadata { LogicalName = "contact" };
            // Attributes must be set via reflection since setter is internal
            SetAttributes(em, new AttributeMetadata[] { attr });

            var mock = MockWithEntitiesResponse(EntityFilters.Attributes, new[] { em });
            var runner = new MetadataQueryRunner(mock.Object, _ => { });

            var result = runner.Run(MakeQuery("attributes"), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]["logicalName"], Is.EqualTo("name"));
            Assert.That(result[0]["entityLogicalName"], Is.EqualTo("contact"));
        }

        [Test]
        public void Run_OptionSets_ReturnsMappedList()
        {
            var os = new OptionSetMetadata(new OptionMetadataCollection()) { Name = "globalos" };
            var mock = new Mock<IOrganizationService>();
            var response = new RetrieveAllOptionSetsResponse();
            response.Results["OptionSetMetadata"] = new OptionSetMetadataBase[] { os };
            mock.Setup(s => s.Execute(It.IsAny<RetrieveAllOptionSetsRequest>())).Returns(response);

            var runner = new MetadataQueryRunner(mock.Object, _ => { });
            var result = runner.Run(MakeQuery("optionsets"), CancellationToken.None);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0]["name"], Is.EqualTo("globalos"));
        }

        [Test]
        public void Run_Relationships_DeduplicatesAcrossEntities()
        {
            var rel = new OneToManyRelationshipMetadata { SchemaName = "account_contact" };
            var em1 = new EntityMetadata { LogicalName = "account" };
            var em2 = new EntityMetadata { LogicalName = "contact" };
            SetOneToMany(em1, new[] { rel });
            SetOneToMany(em2, new[] { rel }); // same relationship referenced by both sides

            var mock = MockWithEntitiesResponse(EntityFilters.Relationships, new[] { em1, em2 });
            var runner = new MetadataQueryRunner(mock.Object, _ => { });

            var result = runner.Run(MakeQuery("relationships"), CancellationToken.None);

            Assert.That(result.Count(r => (string?)r["schemaName"] == "account_contact"), Is.EqualTo(1));
        }

        [Test]
        public void Run_UnknownTarget_ThrowsNotSupportedException()
        {
            var mock = new Mock<IOrganizationService>();
            var runner = new MetadataQueryRunner(mock.Object, _ => { });

            Assert.Throws<NotSupportedException>(() =>
                runner.Run(MakeQuery("unknownTarget"), CancellationToken.None));
        }

        // --- reflection helpers to set internal-setter properties ---

        private static void SetAttributes(EntityMetadata em, AttributeMetadata[] attrs)
        {
            var prop = typeof(EntityMetadata).GetProperty("Attributes",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(em, attrs);
        }

        private static void SetOneToMany(EntityMetadata em, OneToManyRelationshipMetadata[] rels)
        {
            var prop = typeof(EntityMetadata).GetProperty("OneToManyRelationships",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(em, rels);
        }
    }
}
