// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:DiagramTable /&gt;</c> stereotype
    /// application. Records MagicDraw's per-table view configuration
    /// (column ids, widths, custom columns, sort, filter flags).
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.DIAGRAM_TABLE, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class DiagramTable : ProfileElement
    {
        /// <summary>
        /// <c>base_Diagram</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the Diagram this table applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.baseDiagram, Namespace = "")]
        public string? BaseDiagram { get; set; }

        /// <summary><c>showDetailedColumnName</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showDetailedColumnName, Namespace = "")]
        public string? ShowDetailedColumnName { get; set; }

        /// <summary><c>typesIncludeSubtypes</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.typesIncludeSubtypes, Namespace = "")]
        public string? TypesIncludeSubtypes { get; set; }

        /// <summary><c>displayMode</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.displayMode, Namespace = "")]
        public string? DisplayMode { get; set; }

        /// <summary><c>showElementNumber</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showElementNumber, Namespace = "")]
        public string? ShowElementNumber { get; set; }

        /// <summary><c>showColumnIcons</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showColumnIcons, Namespace = "")]
        public string? ShowColumnIcons { get; set; }

        /// <summary><c>showScopeAsRoot</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showScopeAsRoot, Namespace = "")]
        public string? ShowScopeAsRoot { get; set; }

        /// <summary><c>showScope</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showScope, Namespace = "")]
        public string? ShowScope { get; set; }

        /// <summary><c>showFilter</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showFilter, Namespace = "")]
        public string? ShowFilter { get; set; }

        /// <summary><c>showElementType</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showElementType, Namespace = "")]
        public string? ShowElementType { get; set; }

        /// <summary><c>additionalElements</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.additionalElements, Namespace = "")]
        public string? AdditionalElements { get; set; }

        /// <summary><c>&lt;rowElements /&gt;</c> child elements — id refs of the rows.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.ROW_ELEMENTS, Namespace = "")]
        public string[]? RowElements { get; set; }

        /// <summary><c>&lt;sort /&gt;</c> child elements — per-column sort directives.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.SORT, Namespace = "")]
        public string[]? Sorts { get; set; }

        /// <summary><c>&lt;columnIds /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.COLUMN_IDS, Namespace = "")]
        public string[]? ColumnIds { get; set; }

        /// <summary><c>&lt;columnWidth /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.COLUMN_WIDTH, Namespace = "")]
        public string[]? ColumnWidth { get; set; }

        /// <summary><c>&lt;customColumns /&gt;</c> child element (single string blob per upstream v2.8).</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.CUSTOM_COLUMNS, Namespace = "")]
        public string? CustomColumns { get; set; }

        /// <summary><c>includeSubtypesOfRowTypes</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.includeSubtypesOfRowTypes, Namespace = "")]
        public string? IncludeSubtypesOfRowTypes { get; set; }
    }
}
