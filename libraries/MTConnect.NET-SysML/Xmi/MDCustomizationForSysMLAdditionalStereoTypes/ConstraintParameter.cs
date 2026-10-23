// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto
// MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// <c>&lt;MD_Customization_for_SysML__additional_stereotypes:ConstraintParameter /&gt;</c>
    /// stereotype application. Marks a UML Port as a SysML constraint
    /// parameter (a bind-time input / output on a Constraint Block).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.CONSTRAINT_PARAMETER, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
    public class ConstraintParameter : ProfileElement
    {
        /// <summary>
        /// <c>base_Port</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the UML Port carrying the
        /// constraint-parameter stereotype.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.basePort, Namespace = "")]
        public string? BasePort { get; set; }
    }
}
