using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML;
using MTConnect.SysML.CSharp;
using MTConnect.SysML.Models.Devices;
using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.Profile;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.CSharp
{
    /// <summary>
    /// Byte-level golden-fixture coverage on the
    /// <c>[System.Obsolete("Deprecated in v{Version}")]</c> emission path
    /// vendored from mtconnect/MtconnectTranspiler v2.8. Each test pairs an
    /// input XMI fragment (deserialised via <see cref="XmlSerializer"/>) with
    /// an expected <c>.g.cs</c> fixture and asserts byte-identical output
    /// from the appropriate Scriban template.
    /// </summary>
    /// <remarks>
    /// Templates covered:
    /// <list type="bullet">
    ///   <item><c>Model.scriban</c> — concrete class emission</item>
    ///   <item><c>Interface.scriban</c> — interface emission</item>
    ///   <item><c>Devices.ComponentType.scriban</c> — Component subclass emission</item>
    ///   <item><c>Devices.CompositionType.scriban</c> — Composition subclass emission</item>
    ///   <item><c>Devices.DataItemType.scriban</c> — DataItem subclass emission</item>
    ///   <item><c>Interfaces.InterfaceDataItemType.scriban</c> — InterfaceDataItem emission</item>
    /// </list>
    /// The parse-side wiring (<c>Normative.Deprecated</c> attribute →
    /// <see cref="MTConnectClassModel.Deprecated"/>) is exercised via
    /// <see cref="Parse_pulls_Normative_Deprecated_into_MTConnectClassModel"/>
    /// which loads a self-contained XMI fragment.
    /// </remarks>
    [TestFixture]
    public class ObsoleteEmissionTests
    {
        private static string FixturesRoot => Path.Combine(
            TestContext.CurrentContext.TestDirectory, "Fixtures", "Obsolete");

        private static string ReadFixture(string name) =>
            File.ReadAllText(Path.Combine(FixturesRoot, name));

        // ---- Parse-side wiring ----

        [Test]
        public void Parse_pulls_Normative_Deprecated_into_MTConnectClassModel()
        {
            var xmi = ReadFixture("normative-deprecated.xmi");
            var doc = DeserializeXmi(xmi);

            Assert.That(doc, Is.Not.Null, "Fixture must deserialise as XmiDocument");
            var normative = Array.Find(doc.NormativeIntroductions ?? Array.Empty<Normative>(), n => n.BaseElement == "class-deprecated-1");
            Assert.That(normative, Is.Not.Null);
            Assert.That(normative!.Deprecated, Is.EqualTo("2.5"));

            var version = MTConnectVersion.LookupNormativeDeprecated(doc, "class-deprecated-1");
            Assert.That(version, Is.EqualTo("2.5"));
        }

        [Test]
        public void LookupNormativeDeprecated_returns_null_when_element_missing()
        {
            var doc = new XmiDocument { NormativeIntroductions = Array.Empty<Normative>() };
            Assert.That(MTConnectVersion.LookupNormativeDeprecated(doc, "nonexistent"), Is.Null);
        }

        [Test]
        public void LookupNormativeDeprecated_returns_null_when_deprecated_empty()
        {
            var doc = new XmiDocument
            {
                NormativeIntroductions = new[]
                {
                    new Normative { BaseElement = "class-active-1", Deprecated = null },
                    new Normative { BaseElement = "class-active-2", Deprecated = "" },
                }
            };
            Assert.That(MTConnectVersion.LookupNormativeDeprecated(doc, "class-active-1"), Is.Null);
            Assert.That(MTConnectVersion.LookupNormativeDeprecated(doc, "class-active-2"), Is.Null);
        }

        [Test]
        public void LookupNormativeDeprecated_guards_null_document_and_id()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MTConnectVersion.LookupNormativeDeprecated(null, "id"), Is.Null);
                Assert.That(MTConnectVersion.LookupNormativeDeprecated(new XmiDocument(), null), Is.Null);
                Assert.That(MTConnectVersion.LookupNormativeDeprecated(new XmiDocument(), string.Empty), Is.Null);
            });
        }

        // ---- Render-side: Model.scriban ----

        [Test]
        public void Model_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new ClassModel
            {
                Id = "Devices.Foo",
                UmlId = "uml-1",
                Name = "Foo",
                Description = "A deprecated Foo.",
                Deprecated = "2.5",
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("model-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        [Test]
        public void Model_scriban_omits_Obsolete_when_Deprecated_null()
        {
            var model = new ClassModel
            {
                Id = "Devices.Foo",
                UmlId = "uml-1",
                Name = "Foo",
                Description = "An active Foo.",
                Deprecated = null,
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("model-without-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Render-side: Interface.scriban ----

        [Test]
        public void Interface_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new ClassModel
            {
                Id = "Devices.Foo",
                UmlId = "uml-1",
                Name = "Foo",
                Description = "A deprecated Foo.",
                Deprecated = "2.5",
            };

            var rendered = model.RenderInterface();
            var expected = ReadFixture("interface-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Render-side: Devices.ComponentType.scriban ----

        [Test]
        public void ComponentType_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new ComponentType
            {
                Id = "Devices.Components.OldComponent",
                UmlId = "uml-comp-1",
                Name = "OldComponent",
                Type = "OLD",
                DefaultName = "old",
                Description = "A deprecated component.",
                Deprecated = "2.5",
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("component-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Render-side: Devices.CompositionType.scriban ----

        [Test]
        public void CompositionType_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new CompositionType
            {
                Id = "Devices.Compositions.OldComposition",
                UmlId = "uml-comp-2",
                Name = "OldComposition",
                Type = "OLD_COMP",
                DefaultName = "oldComposition",
                Description = "A deprecated composition.",
                Deprecated = "2.5",
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("composition-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Render-side: Devices.DataItemType.scriban ----

        [Test]
        public void DataItemType_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new DataItemType
            {
                Id = "Devices.DataItems.OldDataItem",
                UmlId = "uml-di-1",
                Name = "OldDataItem",
                Type = "OLD_DI",
                Category = "EVENT",
                Description = "A deprecated data item.",
                Deprecated = "2.5",
                SubTypes = new List<MTConnectDataItemSubType>(),
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("dataitem-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Render-side: Interfaces.InterfaceDataItemType.scriban ----

        [Test]
        public void InterfaceDataItemType_scriban_emits_Obsolete_when_Deprecated_set()
        {
            var model = new InterfaceDataItemType
            {
                Id = "Interfaces.InterfaceDataItems.OldInterfaceDataItem",
                UmlId = "uml-idi-1",
                Name = "OldInterfaceDataItem",
                Type = "OLD_IDI",
                Category = "EVENT",
                Description = "A deprecated interface data item.",
                Deprecated = "2.5",
                SubTypes = new List<MTConnectDataItemSubType>(),
            };

            var rendered = model.RenderModel();
            var expected = ReadFixture("interface-dataitem-with-obsolete.expected.g.cs");
            Assert.That(rendered, Is.EqualTo(expected));
        }

        // ---- Helpers ----

        private static XmiDocument DeserializeXmi(string xml)
        {
            var serializer = new XmlSerializer(typeof(XmiDocument));
            using var reader = new StringReader(xml);
            return (XmiDocument)serializer.Deserialize(reader)!;
        }
    }
}
