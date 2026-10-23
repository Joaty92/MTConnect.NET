// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto
// MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// <c>&lt;MD_Customization_for_SysML__additional_stereotypes:ExternalModel /&gt;</c>
    /// stereotype application. Marks a UML Element as sourced from an
    /// external model (so the reference chain is preserved on export).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.EXTERNAL_MODEL, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
    public class ExternalModel : ProfileElement
    {
        /// <summary>
        /// <c>base_Element</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Element carrying the
        /// external-model stereotype.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.baseElement, Namespace = "")]
        public string? BaseElement { get; set; }
    }
}
