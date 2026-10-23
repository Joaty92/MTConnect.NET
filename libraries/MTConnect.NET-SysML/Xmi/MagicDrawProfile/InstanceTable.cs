// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:InstanceTable /&gt;</c> stereotype
    /// application. Records MagicDraw's per-instance-table view
    /// configuration.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.INSTANCE_TABLE, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class InstanceTable : ProfileElement
    {
        /// <summary>
        /// <c>base_Diagram</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the Diagram this instance table
        /// applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.baseDiagram, Namespace = "")]
        public string? BaseDiagram { get; set; }

        /// <summary><c>showColumnIcons</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showColumnIcons, Namespace = "")]
        public string? ShowColumnIcons { get; set; }

        /// <summary><c>classifiers</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.classifiers, Namespace = "")]
        public string? Classifiers { get; set; }

        /// <summary><c>showFilter</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showFilter, Namespace = "")]
        public string? ShowFilter { get; set; }

        /// <summary><c>scope</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.scope, Namespace = "")]
        public string? Scope { get; set; }

        /// <summary><c>showUnitsOnValues</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showUnitsOnValues, Namespace = "")]
        public string? ShowUnitsOnValues { get; set; }

        /// <summary><c>includeSubtypesOfRowTypes</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.includeSubtypesOfRowTypes, Namespace = "")]
        public string? IncludeSubtypesOfRowTypes { get; set; }

        /// <summary><c>showDetailedColumnName</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showDetailedColumnName, Namespace = "")]
        public string? ShowDetailedColumnName { get; set; }

        /// <summary><c>showElementType</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showElementType, Namespace = "")]
        public string? ShowElementType { get; set; }

        /// <summary><c>displayMode</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.displayMode, Namespace = "")]
        public string? DisplayMode { get; set; }

        /// <summary><c>rowsOrder</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.rowsOrder, Namespace = "")]
        public string? RowsOrder { get; set; }

        /// <summary><c>showScope</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showScope, Namespace = "")]
        public string? ShowScope { get; set; }

        /// <summary><c>showScopeAsRoot</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showScopeAsRoot, Namespace = "")]
        public string? ShowScopeAsRoot { get; set; }

        /// <summary><c>showElementNumber</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.showElementNumber, Namespace = "")]
        public string? ShowElementNumber { get; set; }

        /// <summary><c>includeCustomTypesOfRowTypes</c> attribute.</summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.includeCustomTypesOfRowTypes, Namespace = "")]
        public string? IncludeCustomTypesOfRowTypes { get; set; }

        /// <summary><c>&lt;hideColumns /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.HIDE_COLUMNS, Namespace = "")]
        public string[]? HideColumns { get; set; }

        /// <summary><c>&lt;columnIds /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.COLUMN_IDS, Namespace = "")]
        public string[]? ColumnIds { get; set; }

        /// <summary><c>&lt;rowElements /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.ROW_ELEMENTS, Namespace = "")]
        public string[]? RowElements { get; set; }

        /// <summary><c>&lt;expandedRows /&gt;</c> child elements.</summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.EXPANDED_ROWS, Namespace = "")]
        public string[]? ExpandedRows { get; set; }
    }
}
