using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi.ConceptModelingProfile;
using MTConnect.SysML.Xmi.Navigation;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// Coverage on the Concept_Modeling_Profile stereotype classes
    /// vendored from mtconnect/MtconnectTranspiler v2.8. Exercises the
    /// XmlSerializer binding for every stereotype in the family — one
    /// fixture per element the XmiDocument surface exposes — plus the
    /// IdCache side-effect on the shared ProfileElement.Id setter.
    /// </summary>
    [TestFixture]
    public class ConceptModelingProfileStereotypesTests
    {
        [TearDown]
        public void ResetHolder()
        {
            // Guard against a failed test leaving the thread-static
            // holder dirty for the next test in the fixture.
            IdCacheContextHolder.Current = null;
        }

        [Test]
        public void Anything_defaults_are_null()
        {
            var element = new Anything();
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.Null);
                Assert.That(element.BaseClass, Is.Null);
            });
        }

        [Test]
        public void Anything_round_trips_through_XmlSerializer()
        {
            var xml = "<Anything xmi:id=\"a-1\" base_Class=\"c-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<Anything>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("a-1"));
                Assert.That(element.BaseClass, Is.EqualTo("c-1"));
            });
        }

        [Test]
        public void DisjointWith_round_trips_through_XmlSerializer()
        {
            var xml = "<Disjoint_With xmi:id=\"d-1\" base_Dependency=\"dep-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<DisjointWith>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("d-1"));
                Assert.That(element.BaseDependency, Is.EqualTo("dep-1"));
            });
        }

        [Test]
        public void EquivalentClass_round_trips_through_XmlSerializer()
        {
            var xml = "<Equivalent_Class xmi:id=\"e-1\" base_Generalization=\"g-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<EquivalentClass>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("e-1"));
                Assert.That(element.BaseGeneralization, Is.EqualTo("g-1"));
            });
        }

        [Test]
        public void Functional_round_trips_through_XmlSerializer()
        {
            var xml = "<Functional xmi:id=\"f-1\" base_Property=\"p-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<Functional>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("f-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void LiteralAnnotation_round_trips_through_XmlSerializer()
        {
            var xml = "<Literal_Annotation xmi:id=\"l-1\" base_Comment=\"c-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<LiteralAnnotation>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("l-1"));
                Assert.That(element.BaseComment, Is.EqualTo("c-1"));
            });
        }

        [Test]
        public void Resource_round_trips_through_XmlSerializer()
        {
            var xml = "<Resource xmi:id=\"r-1\" base_Class=\"c-1\" base_Property=\"p-1\" IRI=\"http://example.com/r-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<Resource>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("r-1"));
                Assert.That(element.BaseClass, Is.EqualTo("c-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
                Assert.That(element.IRI, Is.EqualTo("http://example.com/r-1"));
            });
        }

        [Test]
        public void Restriction_round_trips_through_XmlSerializer()
        {
            var xml = "<Restriction xmi:id=\"rest-1\" base_Property=\"p-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<Restriction>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("rest-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void Transitive_round_trips_through_XmlSerializer()
        {
            var xml = "<Transitive xmi:id=\"t-1\" base_Property=\"p-1\" xmlns:xmi=\"http://www.omg.org/spec/XMI/20131001\" xmlns=\"http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi\" />";
            var element = Deserialize<Transitive>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("t-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void Id_setter_publishes_to_IdCacheContextHolder()
        {
            using var context = new IdCacheContext();
            var element = new Anything { Id = "cache-me" };
            Assert.That(context.GetFromCache("cache-me"), Is.SameAs(element));
        }

        [Test]
        public void Id_setter_is_a_no_op_when_no_ambient_context()
        {
            // No IdCacheContext active — the setter must still complete
            // without throwing (null-conditional invoke on
            // IdCacheContextHolder.Current).
            Assert.That(IdCacheContextHolder.Current, Is.Null);
            Assert.DoesNotThrow(() =>
            {
                var _ = new Anything { Id = "no-holder" };
            });
        }

        [Test]
        public void Id_setter_skips_empty_id()
        {
            using var context = new IdCacheContext();
            var _ = new Anything { Id = string.Empty };
            Assert.That(context.IdCache, Is.Empty);
        }

        [Test]
        public void Serialise_round_trip_through_XmlSerializer_preserves_attributes()
        {
            var original = new Resource { Id = "r-2", BaseClass = "c-2", IRI = "http://example.com/r-2" };
            var serializer = new XmlSerializer(typeof(Resource));
            using var writer = new StringWriter();
            serializer.Serialize(writer, original);
            var xml = writer.ToString();

            using var reader = new StringReader(xml);
            var roundTripped = (Resource)serializer.Deserialize(reader)!;

            Assert.Multiple(() =>
            {
                Assert.That(roundTripped.Id, Is.EqualTo("r-2"));
                Assert.That(roundTripped.BaseClass, Is.EqualTo("c-2"));
                Assert.That(roundTripped.IRI, Is.EqualTo("http://example.com/r-2"));
            });
        }

        private static T Deserialize<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            return (T)serializer.Deserialize(reader)!;
        }
    }
}
