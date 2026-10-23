// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Anything /&gt;</c> stereotype
    /// application. Applied to a UML Class to declare it as the
    /// concept-model "Anything" root — the top of the concept lattice.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.ANYTHING, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class Anything : ProfileElement
    {
        /// <summary>
        /// <c>base_Class</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Class this stereotype
        /// application applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseClass, Namespace = "")]
        public string? BaseClass { get; set; }
    }
}
