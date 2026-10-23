// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto
// MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes;
// XmlHelper references retargeted to the fork's XmiHelper. Adapted:
// upstream v2.8 has the base_Property attribute mis-namespaced onto the
// Md_Customization_for_SysML__additional_stereotypes namespace, which
// contradicts every sibling PartProperty / ReferenceProperty /
// ConstraintProperty definition in the same profile and the actual
// wire format emitted by MagicDraw. The fork's copy pins Namespace = ""
// so round-trip through XmlSerializer matches the source XMI.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// <c>&lt;MD_Customization_for_SysML__additional_stereotypes:ValueProperty /&gt;</c>
    /// stereotype application. Marks a UML Property as a SysML value
    /// property (a typed value slot on a Block).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.VALUE_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
    public class ValueProperty : ProfileElement
    {
        /// <summary>
        /// <c>base_Property</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Property carrying the
        /// value-property stereotype.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.baseProperty, Namespace = "")]
        public string? BaseProperty { get; set; }
    }
}
