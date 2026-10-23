// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.ConceptModelingProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:Literal_Annotation /&gt;</c>
    /// stereotype application. Applied to a UML Comment to declare its
    /// body a literal annotation on the parent element (rather than
    /// narrative prose).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ConceptModelingProfileStructure.LITERAL_ANNOTATION, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
    public class LiteralAnnotation : ProfileElement
    {
        /// <summary>
        /// <c>base_Comment</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Comment carrying the
        /// literal annotation payload.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ConceptModelingProfileStructure.baseComment, Namespace = "")]
        public string? BaseComment { get; set; }
    }
}
