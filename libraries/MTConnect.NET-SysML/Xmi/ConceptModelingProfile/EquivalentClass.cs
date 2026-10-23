// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Equivalent_Class /&gt;</c>
    /// stereotype application. Applied to a UML Generalization to
    /// declare the two ends as equivalent concepts (bidirectional
    /// subtype relationship).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.EQUIVALENT_CLASS, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class EquivalentClass : ProfileElement
    {
        /// <summary>
        /// <c>base_Generalization</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Generalization carrying
        /// the equivalence relationship.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseGeneralization, Namespace = "")]
        public string? BaseGeneralization { get; set; }
    }
}
