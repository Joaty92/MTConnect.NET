using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.Profile
{
    /// <summary>
    /// <c>&lt;Profile:normative /&gt;</c> element. Widened directly from
    /// mtconnect/MtconnectTranspiler v2.8 with two additions: an
    /// <see cref="Updated"/> string-array child (one entry per MTConnect
    /// version at which the stereotyped element was updated) and a
    /// <see cref="Deprecated"/> attribute (the MTConnect version at
    /// which the element was deprecated). These enable downstream
    /// emission of <c>[Obsolete("Deprecated in vX.Y ...")]</c> attributes
    /// on generated classes carrying <c>NormativeRemarks</c> deprecation
    /// metadata.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.ProfileStructure.NORMATIVE, Namespace = XmiHelper.ProfileNamespace)]
    public class Normative : ProfileElement
    {
        /// <summary>
        /// <c>base_Element</c> attribute
        /// </summary>
        /// <remarks>Foreign key to the <see cref="XmiElement.Id"/> of the object this applies to.</remarks>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.baseElement, Namespace = "")]
        public string? BaseElement { get; set; }

        /// <summary>
        /// <c>introduced</c> attribute — the MTConnect version at which
        /// the stereotyped element was first introduced. Attribute name
        /// preserved from upstream even though the getter is called
        /// <c>Introduced</c> to match the fork's C# naming convention.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.introduced, Namespace = "")]
        public string? Introduced { get; set; }

        /// <summary>
        /// <c>&lt;updated /&gt;</c> child element(s) — one entry per
        /// MTConnect version at which the stereotyped element was
        /// updated. Vendored from mtconnect/MtconnectTranspiler v2.8.
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ProfileStructure.UPDATED, Namespace = "")]
        public string[]? Updated { get; set; }

        /// <summary>
        /// <c>deprecated</c> attribute — the MTConnect version at which
        /// the stereotyped element was deprecated (empty / <c>null</c>
        /// for elements still in active use). Feeds
        /// <c>[Obsolete("Deprecated in vX.Y ...")]</c> attribute
        /// emission on generated classes. Vendored from mtconnect/
        /// MtconnectTranspiler v2.8.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.ProfileStructure.DEPRECATED, Namespace = "")]
        public string? Deprecated { get; set; }
    }
}