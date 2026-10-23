using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.UML
{
    /// <summary>
    /// Coverage on the direct-integrate UmlProperty widening from
    /// mtconnect/MtconnectTranspiler v2.8 — UpperValue (new) and
    /// Extensions[] (previously Extension single). Preserves the
    /// [Obsolete] Extension compatibility accessor's contract.
    /// </summary>
    [TestFixture]
    public class UmlPropertyWideningTests
    {
        [Test]
        public void UmlProperty_UpperValue_default_is_null()
        {
            Assert.That(new UmlProperty().UpperValue, Is.Null);
        }

        [Test]
        public void UmlProperty_UpperValue_set_and_get_round_trip()
        {
            var upper = new UpperValue { Value = "*" };
            var property = new UmlProperty { UpperValue = upper };
            Assert.That(property.UpperValue, Is.SameAs(upper));
            Assert.That(property.UpperValue!.Value, Is.EqualTo("*"));
        }

        [Test]
        public void UmlProperty_Extensions_default_is_null()
        {
            Assert.That(new UmlProperty().Extensions, Is.Null);
        }

        [Test]
        public void UmlProperty_Extensions_preserves_multiple_blocks()
        {
            var property = new UmlProperty
            {
                Extensions = new[]
                {
                    new XmiExtension(),
                    new XmiExtension(),
                    new XmiExtension()
                }
            };
            Assert.That(property.Extensions, Has.Length.EqualTo(3));
        }

#pragma warning disable CS0618 // Explicitly verifying the [Obsolete] compat accessor
        [Test]
        public void UmlProperty_legacy_Extension_returns_first_of_Extensions()
        {
            var first = new XmiExtension();
            var second = new XmiExtension();
            var property = new UmlProperty { Extensions = new[] { first, second } };
            Assert.That(property.Extension, Is.SameAs(first));
        }

        [Test]
        public void UmlProperty_legacy_Extension_returns_null_when_Extensions_empty()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new UmlProperty().Extension, Is.Null);
                Assert.That(new UmlProperty { Extensions = System.Array.Empty<XmiExtension>() }.Extension, Is.Null);
            });
        }
#pragma warning restore CS0618

        [Test]
        public void UmlProperty_DefaultValue_recognises_uml_LiteralInteger()
        {
            const string xml = @"<ownedAttribute xmi:type='uml:Property' xmi:id='p1'
    xmlns:xmi='http://www.omg.org/spec/XMI/20131001'>
    <defaultValue xmi:type='uml:LiteralInteger' xmi:id='dv1' value='42' />
</ownedAttribute>";
            var property = DeserializeProperty(xml);
            Assert.That(property.DefaultValue, Is.InstanceOf<UmlLiteralInteger>());
            Assert.That(((UmlLiteralInteger)property.DefaultValue!).Value, Is.EqualTo(42));
        }

        [Test]
        public void UmlProperty_DefaultValue_recognises_uml_LiteralReal()
        {
            const string xml = @"<ownedAttribute xmi:type='uml:Property' xmi:id='p1'
    xmlns:xmi='http://www.omg.org/spec/XMI/20131001'>
    <defaultValue xmi:type='uml:LiteralReal' xmi:id='dv1' value='3.14' />
</ownedAttribute>";
            var property = DeserializeProperty(xml);
            Assert.That(property.DefaultValue, Is.InstanceOf<UmlLiteralReal>());
            Assert.That(((UmlLiteralReal)property.DefaultValue!).Value, Is.EqualTo(3.14).Within(0.0001));
        }

        [Test]
        public void UmlProperty_DefaultValue_recognises_uml_LiteralBoolean()
        {
            const string xml = @"<ownedAttribute xmi:type='uml:Property' xmi:id='p1'
    xmlns:xmi='http://www.omg.org/spec/XMI/20131001'>
    <defaultValue xmi:type='uml:LiteralBoolean' xmi:id='dv1' value='true' />
</ownedAttribute>";
            var property = DeserializeProperty(xml);
            Assert.That(property.DefaultValue, Is.InstanceOf<UmlLiteralBoolean>());
            Assert.That(((UmlLiteralBoolean)property.DefaultValue!).Value, Is.True);
        }

        [Test]
        public void UmlProperty_DefaultValue_unknown_xmi_type_returns_null()
        {
            const string xml = @"<ownedAttribute xmi:type='uml:Property' xmi:id='p1'
    xmlns:xmi='http://www.omg.org/spec/XMI/20131001'>
    <defaultValue xmi:type='uml:UnknownVariant' xmi:id='dv1' />
</ownedAttribute>";
            var property = DeserializeProperty(xml);
            Assert.That(property.DefaultValue, Is.Null);
        }

        [Test]
        public void UmlProperty_DefaultValue_no_element_returns_null()
        {
            var property = new UmlProperty { DefaultValueElement = null };
            Assert.That(property.DefaultValue, Is.Null);
        }

        private static UmlProperty DeserializeProperty(string xml)
        {
            var serializer = new XmlSerializer(typeof(UmlProperty));
            using var reader = new StringReader(xml);
            return (UmlProperty)serializer.Deserialize(reader)!;
        }
    }
}
