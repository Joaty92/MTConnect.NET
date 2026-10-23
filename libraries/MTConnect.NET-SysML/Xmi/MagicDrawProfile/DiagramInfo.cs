// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:DiagramInfo /&gt;</c> stereotype
    /// application. Captures MagicDraw's per-diagram provenance
    /// metadata (creation / modification dates, author).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.DIAGRAM_INFO, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class DiagramInfo : ProfileElement
    {
        /// <summary>
        /// <c>base_Diagram</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the Diagram this info applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.baseDiagram, Namespace = "")]
        public string? BaseDiagram { get; set; }

        /// <summary>
        /// <c>Creation_date</c> attribute. Human-readable timestamp of
        /// initial diagram creation.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.creationDate, Namespace = "")]
        public string? CreationDate { get; set; }

        /// <summary>
        /// <c>Modification_date</c> attribute. Human-readable timestamp
        /// of the most recent diagram edit.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.modificationDate, Namespace = "")]
        public string? ModificationDate { get; set; }

        /// <summary>
        /// <c>Author</c> attribute. Name of the diagram's original
        /// author as reported by MagicDraw.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.author, Namespace = "")]
        public string? Author { get; set; }

        /// <summary>
        /// <c>Last_modified_by</c> attribute. Name of the most recent
        /// editor.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.lastModifiedBy, Namespace = "")]
        public string? LastModifiedBy { get; set; }
    }
}
