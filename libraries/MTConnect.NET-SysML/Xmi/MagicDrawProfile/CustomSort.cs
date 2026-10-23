// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:CustomSort /&gt;</c> stereotype
    /// application. Records MagicDraw's per-element sort priority so
    /// list views (browser tree, containment tables) preserve the
    /// author's chosen ordering.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.CUSTOM_SORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class CustomSort : ProfileElement
    {
        /// <summary>
        /// <c>base_Element</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the element this custom sort
        /// applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.baseElement, Namespace = "")]
        public string? BaseElement { get; set; }

        /// <summary>
        /// <c>sortPriority</c> attribute — integer-as-string encoding of
        /// the element's sort weight (lower comes first).
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.sortPriority, Namespace = "")]
        public string? SortPriority { get; set; }
    }
}
