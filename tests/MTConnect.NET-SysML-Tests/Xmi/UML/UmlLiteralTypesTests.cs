using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.UML
{
    /// <summary>
    /// Coverage on the three UML literal default-value types vendored from
    /// mtconnect/MtconnectTranspiler v2.8. Each pins the round-trip through
    /// the XmlSerializer surface + the InvariantCulture parse guarantee that
    /// prevents comma-decimal locale drift.
    /// </summary>
    [TestFixture]
    public class UmlLiteralTypesTests
    {
        [Test]
        public void UmlLiteralInteger_type_reports_uml_LiteralInteger()
        {
            var literal = new UmlLiteralInteger();
            Assert.That(literal.Type, Is.EqualTo("uml:LiteralInteger"));
        }

        [TestCase("42", 42)]
        [TestCase("-1", -1)]
        [TestCase("0", 0)]
        public void UmlLiteralInteger_parses_valid_integer(string wire, int expected)
        {
            var literal = new UmlLiteralInteger { ValueSerializable = wire };
            Assert.That(literal.Value, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("not-a-number")]
        [TestCase("3.14")]
        public void UmlLiteralInteger_parses_invalid_input_as_null(string wire)
        {
            var literal = new UmlLiteralInteger { ValueSerializable = wire };
            Assert.That(literal.Value, Is.Null);
        }

        [Test]
        public void UmlLiteralInteger_value_null_serialises_as_null()
        {
            var literal = new UmlLiteralInteger { Value = null };
            Assert.That(literal.ValueSerializable, Is.Null);
        }

        [Test]
        public void UmlLiteralReal_type_reports_uml_LiteralReal()
        {
            var literal = new UmlLiteralReal();
            Assert.That(literal.Type, Is.EqualTo("uml:LiteralReal"));
        }

        [Test]
        public void UmlLiteralReal_uses_invariant_culture_regardless_of_thread_locale()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // uses comma decimal
                var literal = new UmlLiteralReal { ValueSerializable = "3.14" };
                Assert.That(literal.Value, Is.EqualTo(3.14).Within(0.0001));
                // Round-trip out — must not become "3,14" under the de-DE locale.
                Assert.That(literal.ValueSerializable, Does.Contain("3.14"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestCase("")]
        [TestCase("not-a-number")]
        public void UmlLiteralReal_parses_invalid_input_as_null(string wire)
        {
            var literal = new UmlLiteralReal { ValueSerializable = wire };
            Assert.That(literal.Value, Is.Null);
        }

        [Test]
        public void UmlLiteralReal_value_null_serialises_as_null()
        {
            var literal = new UmlLiteralReal { Value = null };
            Assert.That(literal.ValueSerializable, Is.Null);
        }

        [Test]
        public void UmlLiteralBoolean_type_reports_uml_LiteralBoolean()
        {
            var literal = new UmlLiteralBoolean();
            Assert.That(literal.Type, Is.EqualTo("uml:LiteralBoolean"));
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("TRUE", true)]  // bool.TryParse is case-insensitive
        [TestCase("False", false)]
        public void UmlLiteralBoolean_parses_valid_bool(string wire, bool expected)
        {
            var literal = new UmlLiteralBoolean { ValueSerializable = wire };
            Assert.That(literal.Value, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("yes")]
        [TestCase("1")]
        public void UmlLiteralBoolean_parses_invalid_input_as_null(string wire)
        {
            var literal = new UmlLiteralBoolean { ValueSerializable = wire };
            Assert.That(literal.Value, Is.Null);
        }

        [Test]
        public void UmlLiteralBoolean_emits_lowercase_true_false()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new UmlLiteralBoolean { Value = true }.ValueSerializable, Is.EqualTo("true"));
                Assert.That(new UmlLiteralBoolean { Value = false }.ValueSerializable, Is.EqualTo("false"));
                Assert.That(new UmlLiteralBoolean { Value = null }.ValueSerializable, Is.Null);
            });
        }
    }
}
