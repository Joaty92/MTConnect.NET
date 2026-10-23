using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.UML
{
    /// <summary>
    /// <inheritdoc cref="MTConnect.SysML.Xmi.OwnedRule"/> where <c>xmi:type='uml:Constraint'</c>.
    /// The fork previously carried only <see cref="ConstrainedElement"/> and left the OCL body
    /// buried in the inherited <see cref="OwnedRule.Specification"/> path — surfaced explicitly
    /// via <see cref="Body"/> and <see cref="Language"/> here so downstream Rules[] emission on
    /// generated classes can consult the raw OCL expression without walking two levels of
    /// nested types. This is the direct-integrate widening equivalent to the constraint-body
    /// preservation shape in mtconnect/MtconnectTranspiler v2.8.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.XmiStructure.OWNED_RULE, Namespace = "")]
    public class UmlConstraint : OwnedRule
    {
        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.ConstrainedElement"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.CONSTRAINED_ELEMENT, Namespace = "")]
        public ConstrainedElement? ConstrainedElement { get; set; }

        /// <summary>
        /// Raw OCL body of the constraint, sourced from
        /// <see cref="OwnedRule.Specification"/>.<see cref="Specification.Body"/>. Returns
        /// <c>null</c> when no specification child is present or when its body is empty.
        /// Vendored preservation of the upstream Constraint.Body semantics — downstream
        /// generators can round-trip the raw OCL expression onto a <c>Rules[]</c> property
        /// on the generated class instead of throwing it away.
        /// </summary>
        [XmlIgnore]
        public string? Body => Specification?.Body;

        /// <summary>
        /// Constraint body language declaration (typically <c>OCL</c>). Sourced from
        /// <see cref="OwnedRule.Specification"/>.<see cref="Specification.Language"/>.
        /// Returns <c>null</c> when the specification omits the language element — a
        /// callable that treats an omitted language as OCL is a reasonable default per
        /// the OMG UML spec, but the omission is preserved here so callers can decide.
        /// </summary>
        [XmlIgnore]
        public string? Language => Specification?.Language;
    }
}