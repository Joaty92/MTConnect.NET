// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Resource /&gt;</c> stereotype
    /// application. Applied to a UML Class or Property to publish it as
    /// an IRI-addressable concept-model resource (used for linked-data
    /// exports).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.RESOURCE, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class Resource : ProfileElement
    {
        /// <summary>
        /// <c>base_Class</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Class this resource
        /// stereotype applies to (mutually exclusive with
        /// <see cref="BaseProperty"/> in practice — the source XMI only
        /// populates one).
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseClass, Namespace = "")]
        public string? BaseClass { get; set; }

        /// <summary>
        /// <c>base_Property</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Property this resource
        /// stereotype applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseProperty, Namespace = "")]
        public string? BaseProperty { get; set; }

        /// <summary>
        /// <c>IRI</c> attribute. Absolute IRI at which the stereotyped
        /// concept is published (empty for concepts that inherit their
        /// IRI from the enclosing resource namespace).
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.IRI, Namespace = "")]
        public string? IRI { get; set; }
    }
}
