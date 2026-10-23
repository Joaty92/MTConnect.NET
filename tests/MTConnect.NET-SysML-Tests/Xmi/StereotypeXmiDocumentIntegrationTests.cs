using System.Threading;
using MTConnect.SysML.Xmi;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi
{
    /// <summary>
    /// End-to-end coverage on the XmiDocument registration surface for
    /// the vendored MagicDraw + Cameo Concept Modeling + MDCustomization
    /// stereotype families. Feeds an XMI fixture that carries at least
    /// one element from every registered stereotype family through the
    /// full <see cref="XmiDeserializer"/> pipeline and asserts each
    /// array on <see cref="XmiDocument"/> pops populated.
    /// </summary>
    [TestFixture]
    public class StereotypeXmiDocumentIntegrationTests
    {
        private const string Xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xmi:XMI xmi:version=""2.1""
         xmlns:xmi=""http://www.omg.org/spec/XMI/20131001""
         xmlns:uml=""http://www.omg.org/spec/UML/20131001""
         xmlns:Concept_Modeling_Profile=""http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi""
         xmlns:MagicDraw_Profile=""http://www.omg.org/spec/UML/20131001/MagicDrawProfile""
         xmlns:MD_Customization_for_SysML__additional_stereotypes=""http://www.magicdraw.com/spec/Customization/180/SysML"">

  <Concept_Modeling_Profile:Anything             xmi:id=""a-1""    base_Class=""c-1"" />
  <Concept_Modeling_Profile:Disjoint_With        xmi:id=""d-1""    base_Dependency=""dep-1"" />
  <Concept_Modeling_Profile:Equivalent_Class     xmi:id=""e-1""    base_Generalization=""g-1"" />
  <Concept_Modeling_Profile:Functional           xmi:id=""f-1""    base_Property=""p-1"" />
  <Concept_Modeling_Profile:Literal_Annotation   xmi:id=""la-1""   base_Comment=""com-1"" />
  <Concept_Modeling_Profile:Resource             xmi:id=""r-1""    base_Class=""c-2"" IRI=""http://example.com/r-1"" />
  <Concept_Modeling_Profile:Restriction          xmi:id=""rest-1"" base_Property=""p-2"" />
  <Concept_Modeling_Profile:Transitive           xmi:id=""t-1""    base_Property=""p-3"" />

  <MagicDraw_Profile:additionalElementImport     xmi:id=""aei-1""  base_ElementImport=""ei-1"" treatAsAuxiliaryInOwningProject=""true"" />
  <MagicDraw_Profile:additionalPackageImport     xmi:id=""api-1""  base_PackageImport=""pi-1"" />
  <MagicDraw_Profile:CustomSort                  xmi:id=""cs-1""   base_Element=""el-1"" sortPriority=""7"" />
  <MagicDraw_Profile:DiagramInfo                 xmi:id=""di-1""   base_Diagram=""d-1"" Author=""alice"" />
  <MagicDraw_Profile:DiagramTable                xmi:id=""dt-1""   base_Diagram=""d-2"" />
  <MagicDraw_Profile:InstanceTable               xmi:id=""it-1""   base_Diagram=""d-3"" />

  <MD_Customization_for_SysML__additional_stereotypes:ValueProperty       xmi:id=""vp-1""   base_Property=""p-10"" />
  <MD_Customization_for_SysML__additional_stereotypes:PartProperty        xmi:id=""pp-1""   base_Property=""p-11"" />
  <MD_Customization_for_SysML__additional_stereotypes:ReferenceProperty   xmi:id=""rp-1""   base_Property=""p-12"" />
  <MD_Customization_for_SysML__additional_stereotypes:ConstraintProperty  xmi:id=""cp-1""   base_Property=""p-13"" />
  <MD_Customization_for_SysML__additional_stereotypes:ConstraintParameter xmi:id=""cpm-1""  base_Port=""port-1"" />
  <MD_Customization_for_SysML__additional_stereotypes:ExternalModel       xmi:id=""em-1""   base_Element=""el-2"" />

</xmi:XMI>";

        [Test]
        public void XmiDocument_surfaces_every_vendored_stereotype_family()
        {
            var deserialiser = XmiDeserializer.FromXml(Xml);
            var document = deserialiser.Deserialize(CancellationToken.None);

            Assert.That(document, Is.Not.Null);

            Assert.Multiple(() =>
            {
                // Concept_Modeling_Profile
                Assert.That(document!.Anythings, Has.Length.EqualTo(1));
                Assert.That(document.Anythings![0].BaseClass, Is.EqualTo("c-1"));
                Assert.That(document.DisjointsWith, Has.Length.EqualTo(1));
                Assert.That(document.DisjointsWith![0].BaseDependency, Is.EqualTo("dep-1"));
                Assert.That(document.EquivalentClasses, Has.Length.EqualTo(1));
                Assert.That(document.EquivalentClasses![0].BaseGeneralization, Is.EqualTo("g-1"));
                Assert.That(document.Functionals, Has.Length.EqualTo(1));
                Assert.That(document.Functionals![0].BaseProperty, Is.EqualTo("p-1"));
                Assert.That(document.LiteralAnnotations, Has.Length.EqualTo(1));
                Assert.That(document.LiteralAnnotations![0].BaseComment, Is.EqualTo("com-1"));
                Assert.That(document.Resources, Has.Length.EqualTo(1));
                Assert.That(document.Resources![0].IRI, Is.EqualTo("http://example.com/r-1"));
                Assert.That(document.Restrictions, Has.Length.EqualTo(1));
                Assert.That(document.Restrictions![0].BaseProperty, Is.EqualTo("p-2"));
                Assert.That(document.Transitives, Has.Length.EqualTo(1));
                Assert.That(document.Transitives![0].BaseProperty, Is.EqualTo("p-3"));

                // MagicDraw_Profile
                Assert.That(document.AdditionalElementImports, Has.Length.EqualTo(1));
                Assert.That(document.AdditionalElementImports![0].BaseElementImport, Is.EqualTo("ei-1"));
                Assert.That(document.AdditionalPackageImports, Has.Length.EqualTo(1));
                Assert.That(document.AdditionalPackageImports![0].BasePackageImport, Is.EqualTo("pi-1"));
                Assert.That(document.CustomSorts, Has.Length.EqualTo(1));
                Assert.That(document.CustomSorts![0].SortPriority, Is.EqualTo("7"));
                Assert.That(document.DiagramInfos, Has.Length.EqualTo(1));
                Assert.That(document.DiagramInfos![0].Author, Is.EqualTo("alice"));
                Assert.That(document.DiagramTables, Has.Length.EqualTo(1));
                Assert.That(document.DiagramTables![0].BaseDiagram, Is.EqualTo("d-2"));
                Assert.That(document.InstanceTables, Has.Length.EqualTo(1));
                Assert.That(document.InstanceTables![0].BaseDiagram, Is.EqualTo("d-3"));

                // MD_Customization_for_SysML__additional_stereotypes
                Assert.That(document.ValueProperties, Has.Length.EqualTo(1));
                Assert.That(document.ValueProperties![0].BaseProperty, Is.EqualTo("p-10"));
                Assert.That(document.PartProperties, Has.Length.EqualTo(1));
                Assert.That(document.PartProperties![0].BaseProperty, Is.EqualTo("p-11"));
                Assert.That(document.ReferenceProperties, Has.Length.EqualTo(1));
                Assert.That(document.ReferenceProperties![0].BaseProperty, Is.EqualTo("p-12"));
                Assert.That(document.ConstraintProperties, Has.Length.EqualTo(1));
                Assert.That(document.ConstraintProperties![0].BaseProperty, Is.EqualTo("p-13"));
                Assert.That(document.ConstraintParameters, Has.Length.EqualTo(1));
                Assert.That(document.ConstraintParameters![0].BasePort, Is.EqualTo("port-1"));
                Assert.That(document.ExternalModels, Has.Length.EqualTo(1));
                Assert.That(document.ExternalModels![0].BaseElement, Is.EqualTo("el-2"));
            });
        }
    }
}
