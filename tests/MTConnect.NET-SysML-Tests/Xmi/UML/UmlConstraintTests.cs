using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.UML
{
    /// <summary>
    /// Coverage on the direct-integrate UmlConstraint widening surfacing
    /// the OCL body + language from the inherited Specification chain.
    /// </summary>
    [TestFixture]
    public class UmlConstraintTests
    {
        [Test]
        public void Body_returns_specification_body_when_present()
        {
            var constraint = new UmlConstraint
            {
                Specification = new Specification { Body = "self.value > 0" }
            };
            Assert.That(constraint.Body, Is.EqualTo("self.value > 0"));
        }

        [Test]
        public void Body_returns_null_when_specification_is_null()
        {
            var constraint = new UmlConstraint { Specification = null };
            Assert.That(constraint.Body, Is.Null);
        }

        [Test]
        public void Body_returns_null_when_specification_body_is_null()
        {
            var constraint = new UmlConstraint
            {
                Specification = new Specification { Body = null }
            };
            Assert.That(constraint.Body, Is.Null);
        }

        [Test]
        public void Language_returns_specification_language_when_present()
        {
            var constraint = new UmlConstraint
            {
                Specification = new Specification { Language = "OCL" }
            };
            Assert.That(constraint.Language, Is.EqualTo("OCL"));
        }

        [Test]
        public void Language_returns_null_when_specification_is_null()
        {
            Assert.That(new UmlConstraint().Language, Is.Null);
        }
    }
}
