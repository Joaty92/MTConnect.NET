using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML.Xmi.Profile;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.Profile
{
    /// <summary>
    /// Coverage on the direct-integrate Normative widening carrying the
    /// Updated[] and Deprecated fields vendored from mtconnect/
    /// MtconnectTranspiler v2.8. Enables downstream NormativeRemarks ->
    /// [Obsolete] emission on generated classes.
    /// </summary>
    [TestFixture]
    public class NormativeWideningTests
    {
        [Test]
        public void Updated_default_is_null()
        {
            Assert.That(new Normative().Updated, Is.Null);
        }

        [Test]
        public void Deprecated_default_is_null()
        {
            Assert.That(new Normative().Deprecated, Is.Null);
        }

        [Test]
        public void Updated_set_and_get_round_trip()
        {
            var normative = new Normative
            {
                Updated = new[] { "1.4", "1.5", "2.0" }
            };
            Assert.That(normative.Updated, Is.EqualTo(new[] { "1.4", "1.5", "2.0" }));
        }

        [Test]
        public void Deprecated_set_and_get_round_trip()
        {
            var normative = new Normative { Deprecated = "2.5" };
            Assert.That(normative.Deprecated, Is.EqualTo("2.5"));
        }

        [Test]
        public void Serialises_Updated_and_Deprecated_via_XmlSerializer()
        {
            var normative = new Normative
            {
                Introduced = "1.0",
                Deprecated = "2.5",
                Updated = new[] { "1.4", "2.0" }
            };
            var serializer = new XmlSerializer(typeof(Normative));
            using var writer = new StringWriter();
            serializer.Serialize(writer, normative);
            var xml = writer.ToString();

            Assert.Multiple(() =>
            {
                Assert.That(xml, Does.Contain("introduced=\"1.0\""));
                Assert.That(xml, Does.Contain("deprecated=\"2.5\""));
                Assert.That(xml, Does.Match("<updated[^>]*>1\\.4</updated>"));
                Assert.That(xml, Does.Match("<updated[^>]*>2\\.0</updated>"));
            });
        }
    }
}
