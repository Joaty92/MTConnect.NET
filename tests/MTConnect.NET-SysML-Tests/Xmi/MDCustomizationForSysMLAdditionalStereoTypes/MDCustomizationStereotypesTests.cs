using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes;
using MTConnect.SysML.Xmi.Navigation;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// Coverage on the MD_Customization_for_SysML__additional_stereotypes
    /// stereotype classes vendored from mtconnect/MtconnectTranspiler v2.8.
    /// Exercises the XmlSerializer binding for every stereotype in the
    /// family plus the IdCache side-effect on the shared
    /// ProfileElement.Id setter.
    /// </summary>
    [TestFixture]
    public class MDCustomizationStereotypesTests
    {
        private const string MdcNs = "http://www.magicdraw.com/spec/Customization/180/SysML";
        private const string XmiNs = "http://www.omg.org/spec/XMI/20131001";

        [TearDown]
        public void ResetHolder()
        {
            IdCacheContextHolder.Current = null;
        }

        [Test]
        public void ValueProperty_round_trips()
        {
            var xml = $"<ValueProperty xmi:id=\"vp-1\" base_Property=\"p-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<ValueProperty>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("vp-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void PartProperty_round_trips()
        {
            var xml = $"<PartProperty xmi:id=\"pp-1\" base_Property=\"p-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<PartProperty>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("pp-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void ReferenceProperty_round_trips()
        {
            var xml = $"<ReferenceProperty xmi:id=\"rp-1\" base_Property=\"p-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<ReferenceProperty>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("rp-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void ConstraintProperty_round_trips()
        {
            var xml = $"<ConstraintProperty xmi:id=\"cp-1\" base_Property=\"p-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<ConstraintProperty>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("cp-1"));
                Assert.That(element.BaseProperty, Is.EqualTo("p-1"));
            });
        }

        [Test]
        public void ConstraintParameter_round_trips()
        {
            var xml = $"<ConstraintParameter xmi:id=\"cpm-1\" base_Port=\"port-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<ConstraintParameter>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("cpm-1"));
                Assert.That(element.BasePort, Is.EqualTo("port-1"));
            });
        }

        [Test]
        public void ExternalModel_round_trips()
        {
            var xml = $"<ExternalModel xmi:id=\"em-1\" base_Element=\"e-1\" xmlns:xmi=\"{XmiNs}\" xmlns=\"{MdcNs}\" />";
            var element = Deserialize<ExternalModel>(xml);
            Assert.Multiple(() =>
            {
                Assert.That(element.Id, Is.EqualTo("em-1"));
                Assert.That(element.BaseElement, Is.EqualTo("e-1"));
            });
        }

        [Test]
        public void Defaults_are_null_on_new_instances()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new ValueProperty().Id, Is.Null);
                Assert.That(new ValueProperty().BaseProperty, Is.Null);
                Assert.That(new PartProperty().BaseProperty, Is.Null);
                Assert.That(new ReferenceProperty().BaseProperty, Is.Null);
                Assert.That(new ConstraintProperty().BaseProperty, Is.Null);
                Assert.That(new ConstraintParameter().BasePort, Is.Null);
                Assert.That(new ExternalModel().BaseElement, Is.Null);
            });
        }

        [Test]
        public void ValueProperty_base_property_serialises_unnamespaced()
        {
            // Regression guard: upstream v2.8's ValueProperty.cs put the
            // base_Property attribute on the
            // Md_Customization_for_SysML__additional_stereotypes namespace,
            // which contradicts every sibling stereotype in the family
            // and the MagicDraw wire format. Fork's adaptation pins the
            // namespace to "" (empty). This test locks that behaviour so
            // a future vendoring refresh cannot silently regress.
            var original = new ValueProperty { Id = "vp-2", BaseProperty = "p-2" };
            var serializer = new XmlSerializer(typeof(ValueProperty));
            using var writer = new StringWriter();
            serializer.Serialize(writer, original);
            var xml = writer.ToString();

            Assert.That(xml, Does.Contain(" base_Property=\"p-2\""));
            Assert.That(xml, Does.Not.Contain("MD_Customization_for_SysML"));
        }

        [Test]
        public void Id_setter_publishes_to_IdCacheContextHolder()
        {
            using var context = new IdCacheContext();
            var element = new ValueProperty { Id = "cache-me" };
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
