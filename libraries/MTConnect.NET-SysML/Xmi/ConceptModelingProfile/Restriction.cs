// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Restriction /&gt;</c> stereotype
    /// application. Applied to a UML Property to narrow its permissible
    /// range within the concept model.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.RESTRICTION, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class Restriction : ProfileElement
    {
        /// <summary>
        /// <c>base_Property</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Property being
        /// restricted.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseProperty, Namespace = "")]
        public string? BaseProperty { get; set; }
    }
}
