// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto
// MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// <c>&lt;MD_Customization_for_SysML__additional_stereotypes:PartProperty /&gt;</c>
    /// stereotype application. Marks a UML Property as a SysML part
    /// property (a composite-owned subcomponent slot on a Block).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.PART_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
    public class PartProperty : ProfileElement
    {
        /// <summary>
        /// <c>base_Property</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Property carrying the
        /// part-property stereotype.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.baseProperty, Namespace = "")]
        public string? BaseProperty { get; set; }
    }
}
