using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi.MagicDrawProfile;
using MTConnect.SysML.Xmi.Navigation;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// Coverage on the MagicDraw_Profile stereotype classes vendored
    /// from mtconnect/MtconnectTranspiler v2.8. Exercises the XmlSerializer
    /// binding for every stereotype in the family plus the IdCache
    /// side-effect on the shared ProfileElement.Id setter.
    /// </summary>
    [TestFixture]
    public class MagicDrawProfileStereotypesTests
    {
        private const string MdNs = "http://www.omg.org/spec/UML/20131001/MagicDrawProfile";
        private const string XmiNs = "http://www.omg.org/spec/XMI/20131001";

        [TearDown]
        public void ResetHolder()
        {
            IdCacheContextHolder.Current = null;
        }

        [Test]
        public void AdditionalElementImport_round_trips()
        {
            var xml = $"<additionalElementImport xmi:id=\"ai-1\" base_ElementImport=\"e-1\" treatAsAuxiliaryInOwningProject=\"true\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\" />";
            var element = Deserialize<AdditionalElementImport>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("ai-1"));
                Assert.That(element.BaseElementImport, Is.EqualTo("e-1"));
                Assert.That(element.TreatAsAuxiliaryInOwningProject, Is.EqualTo("true"));
            });
        }

        [Test]
        public void AdditionalPackageImport_round_trips()
        {
            var xml = $"<additionalPackageImport xmi:id=\"api-1\" base_PackageImport=\"p-1\" treatAsAuxiliaryInOwningProject=\"false\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\" />";
            var element = Deserialize<AdditionalPackageImport>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("api-1"));
                Assert.That(element.BasePackageImport, Is.EqualTo("p-1"));
                Assert.That(element.TreatAsAuxiliaryInOwningProject, Is.EqualTo("false"));
            });
        }

        [Test]
        public void CustomSort_round_trips()
        {
            var xml = $"<CustomSort xmi:id=\"cs-1\" base_Element=\"e-1\" sortPriority=\"42\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\" />";
            var element = Deserialize<CustomSort>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("cs-1"));
                Assert.That(element.BaseElement, Is.EqualTo("e-1"));
                Assert.That(element.SortPriority, Is.EqualTo("42"));
            });
        }

        [Test]
        public void DiagramInfo_round_trips_all_attributes()
        {
            var xml = $"<DiagramInfo xmi:id=\"di-1\" base_Diagram=\"d-1\" Creation_date=\"2020-01-02\" Modification_date=\"2021-03-04\" Author=\"alice\" Last_modified_by=\"bob\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\" />";
            var element = Deserialize<DiagramInfo>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("di-1"));
                Assert.That(element.BaseDiagram, Is.EqualTo("d-1"));
                Assert.That(element.CreationDate, Is.EqualTo("2020-01-02"));
                Assert.That(element.ModificationDate, Is.EqualTo("2021-03-04"));
                Assert.That(element.Author, Is.EqualTo("alice"));
                Assert.That(element.LastModifiedBy, Is.EqualTo("bob"));
            });
        }

        [Test]
        public void DiagramTable_round_trips_attributes_and_children()
        {
            var xml = $"<DiagramTable xmi:id=\"dt-1\" base_Diagram=\"d-1\" showDetailedColumnName=\"true\" typesIncludeSubtypes=\"false\" displayMode=\"grid\" showElementNumber=\"true\" showColumnIcons=\"false\" showScopeAsRoot=\"true\" showScope=\"false\" showFilter=\"true\" showElementType=\"false\" additionalElements=\"none\" includeSubtypesOfRowTypes=\"false\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\">"
                    + "<rowElements xmlns=\"\">r-1</rowElements>"
                    + "<rowElements xmlns=\"\">r-2</rowElements>"
                    + "<sort xmlns=\"\">asc:col-1</sort>"
                    + "<columnIds xmlns=\"\">col-1</columnIds>"
                    + "<columnWidth xmlns=\"\">120</columnWidth>"
                    + "<customColumns xmlns=\"\">blob</customColumns>"
                    + "</DiagramTable>";
            var element = Deserialize<DiagramTable>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("dt-1"));
                Assert.That(element.BaseDiagram, Is.EqualTo("d-1"));
                Assert.That(element.ShowDetailedColumnName, Is.EqualTo("true"));
                Assert.That(element.TypesIncludeSubtypes, Is.EqualTo("false"));
                Assert.That(element.DisplayMode, Is.EqualTo("grid"));
                Assert.That(element.ShowElementNumber, Is.EqualTo("true"));
                Assert.That(element.ShowColumnIcons, Is.EqualTo("false"));
                Assert.That(element.ShowScopeAsRoot, Is.EqualTo("true"));
                Assert.That(element.ShowScope, Is.EqualTo("false"));
                Assert.That(element.ShowFilter, Is.EqualTo("true"));
                Assert.That(element.ShowElementType, Is.EqualTo("false"));
                Assert.That(element.AdditionalElements, Is.EqualTo("none"));
                Assert.That(element.IncludeSubtypesOfRowTypes, Is.EqualTo("false"));
                Assert.That(element.RowElements, Is.EqualTo(new[] { "r-1", "r-2" }));
                Assert.That(element.Sorts, Is.EqualTo(new[] { "asc:col-1" }));
                Assert.That(element.ColumnIds, Is.EqualTo(new[] { "col-1" }));
                Assert.That(element.ColumnWidth, Is.EqualTo(new[] { "120" }));
                Assert.That(element.CustomColumns, Is.EqualTo("blob"));
            });
        }

        [Test]
        public void InstanceTable_round_trips_attributes_and_children()
        {
            var xml = $"<InstanceTable xmi:id=\"it-1\" base_Diagram=\"d-1\" showColumnIcons=\"true\" classifiers=\"c-1 c-2\" showFilter=\"false\" scope=\"local\" showUnitsOnValues=\"true\" includeSubtypesOfRowTypes=\"false\" showDetailedColumnName=\"true\" showElementType=\"false\" displayMode=\"list\" rowsOrder=\"asc\" showScope=\"true\" showScopeAsRoot=\"false\" showElementNumber=\"true\" includeCustomTypesOfRowTypes=\"false\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdNs}\">"
                    + "<hideColumns xmlns=\"\">hc-1</hideColumns>"
                    + "<columnIds xmlns=\"\">col-1</columnIds>"
                    + "<rowElements xmlns=\"\">r-1</rowElements>"
                    + "<expandedRows xmlns=\"\">er-1</expandedRows>"
                    + "</InstanceTable>";
            var element = Deserialize<InstanceTable>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("it-1"));
                Assert.That(element.BaseDiagram, Is.EqualTo("d-1"));
                Assert.That(element.ShowColumnIcons, Is.EqualTo("true"));
                Assert.That(element.Classifiers, Is.EqualTo("c-1 c-2"));
                Assert.That(element.ShowFilter, Is.EqualTo("false"));
                Assert.That(element.Scope, Is.EqualTo("local"));
                Assert.That(element.ShowUnitsOnValues, Is.EqualTo("true"));
                Assert.That(element.IncludeSubtypesOfRowTypes, Is.EqualTo("false"));
                Assert.That(element.ShowDetailedColumnName, Is.EqualTo("true"));
                Assert.That(element.ShowElementType, Is.EqualTo("false"));
                Assert.That(element.DisplayMode, Is.EqualTo("list"));
                Assert.That(element.RowsOrder, Is.EqualTo("asc"));
                Assert.That(element.ShowScope, Is.EqualTo("true"));
                Assert.That(element.ShowScopeAsRoot, Is.EqualTo("false"));
                Assert.That(element.ShowElementNumber, Is.EqualTo("true"));
                Assert.That(element.IncludeCustomTypesOfRowTypes, Is.EqualTo("false"));
                Assert.That(element.HideColumns, Is.EqualTo(new[] { "hc-1" }));
                Assert.That(element.ColumnIds, Is.EqualTo(new[] { "col-1" }));
                Assert.That(element.RowElements, Is.EqualTo(new[] { "r-1" }));
                Assert.That(element.ExpandedRows, Is.EqualTo(new[] { "er-1" }));
            });
        }

        [Test]
        public void Defaults_are_null_on_new_instances()
        {
            var ai = new AdditionalElementImport();
            var api = new AdditionalPackageImport();
            var cs = new CustomSort();
            var di = new DiagramInfo();
            var dt = new DiagramTable();
            var it = new InstanceTable();
            Assert.Multiple(() =>
            {
                Assert.That(ai.Id, Is.Null);
                Assert.That(ai.BaseElementImport, Is.Null);
                Assert.That(ai.TreatAsAuxiliaryInOwningProject, Is.Null);
                Assert.That(api.BasePackageImport, Is.Null);
                Assert.That(cs.BaseElement, Is.Null);
                Assert.That(cs.SortPriority, Is.Null);
                Assert.That(di.Author, Is.Null);
                Assert.That(dt.RowElements, Is.Null);
                Assert.That(it.HideColumns, Is.Null);
            });
        }

        [Test]
        public void Id_setter_publishes_to_IdCacheContextHolder()
        {
            using var context = new IdCacheContext();
            var element = new DiagramInfo { Id = "cache-me" };
            Assert.That(context.GetFromCache("cache-me"), Is.SameAs(element));
        }

        private static T Deserialize<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            return (T)serializer.Deserialize(reader)!;
        }
    }
}
