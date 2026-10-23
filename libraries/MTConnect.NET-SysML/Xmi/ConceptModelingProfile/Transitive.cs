// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Transitive /&gt;</c> stereotype
    /// application. Applied to a UML Property to declare the relation
    /// transitive (if a-P-b and b-P-c then a-P-c holds implicitly).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.TRANSITIVE, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class Transitive : ProfileElement
    {
        /// <summary>
        /// <c>base_Property</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Property carrying the
        /// transitivity declaration.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseProperty, Namespace = "")]
        public string? BaseProperty { get; set; }
    }
}
