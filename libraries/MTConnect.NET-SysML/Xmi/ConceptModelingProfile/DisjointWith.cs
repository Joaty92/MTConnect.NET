// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Disjoint_With /&gt;</c> stereotype
    /// application. Applied to a UML Dependency to declare its endpoints
    /// as disjoint concepts (no instance may satisfy both).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.DISJOINT_WITH, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class DisjointWith : ProfileElement
    {
        /// <summary>
        /// <c>base_Dependency</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Dependency carrying the
        /// disjointness relationship.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseDependency, Namespace = "")]
        public string? BaseDependency { get; set; }
    }
}
