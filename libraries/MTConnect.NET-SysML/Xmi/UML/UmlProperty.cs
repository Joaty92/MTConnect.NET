using System;
using System.Xml;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.UML
{
    /// <summary>
    /// <inheritdoc cref="MTConnect.SysML.Xmi.OwnedAttribute"/> where <c>xmi:type='uml:Property'</c>
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.XmiStructure.OWNED_ATTRIBUTE, Namespace = "")]
    public class UmlProperty : OwnedAttribute
    {
        /// <inheritdoc cref="MTConnect.SysML.Xmi.XmiElement.Type"/>
        public override string Type => XmiHelper.UmlStructure.Property;

        /// <summary>
        /// <c>association</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.association, Namespace = "")]
        public string? Association { get; set; }

        // TODO: Lookup the uml:Association[@name] to determine the expected Property Name
        // TODO: Figure out how to determine if the associated type is an array. Possibly just a reference to the lowerValue/upperValue elements

        /// <summary>
        /// <c>aggregation</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.aggregation, Namespace = "")]
        public string? Aggregation { get; set; }

        /// <summary>
        /// <c>type</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.type, Namespace = "")]
        public string? PropertyType { get; set; }

        /// <summary>
        /// <c>isStatic</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.isStatic, Namespace = "")]
        public bool IsStatic { get; set; }

        /// <summary>
        /// <c>isReadOnly</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.isReadOnly, Namespace = "")]
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.LowerValue"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.LOWER_VALUE, Namespace = "")]
        public LowerValue? LowerValue { get; set; }

        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.UpperValue"/>. Vendored
        /// from mtconnect/MtconnectTranspiler v2.8 so per-property multiplicity
        /// upper-bounds ('1', '*', '2..5' upper leg, etc.) are preserved on
        /// generated classes rather than dropped on the floor. The fork
        /// previously exposed only <see cref="LowerValue"/>; downstream
        /// pipeline code that emits property multiplicity should now consult
        /// both.
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.UPPER_VALUE, Namespace = "")]
        public UpperValue? UpperValue { get; set; }

        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.DefaultValue"/>
        /// </summary>
        [XmlAnyElement(XmiHelper.XmiStructure.DEFAULT_VALUE, Namespace = "")]
        public XmlElement? DefaultValueElement { get; set; }
        private DefaultValue? _defaultValue;
        /// <summary>
        /// The deserialized default value of the property, materialized
        /// lazily from <see cref="DefaultValueElement"/>. Recognises five
        /// <c>xmi:type</c> variants — the fork's original
        /// <see cref="UmlInstanceValue"/> + <see cref="UmlLiteralString"/>
        /// plus <see cref="UmlLiteralInteger"/> / <see cref="UmlLiteralReal"/>
        /// / <see cref="UmlLiteralBoolean"/> vendored from
        /// mtconnect/MtconnectTranspiler v2.8. Returns <c>null</c> when no
        /// default value element is present, or when the <c>xmi:type</c>
        /// falls outside the recognised set (unknown types are ignored
        /// rather than swallowed silently — call sites can distinguish
        /// "no default declared" from "unknown default type" by inspecting
        /// <see cref="DefaultValueElement"/> directly).
        /// </summary>
        public DefaultValue? DefaultValue
        {
            get
            {
                if (_defaultValue != null)
                    return _defaultValue;
                if (DefaultValueElement == null)
                    return null;

                XmlRootAttribute xRoot = new XmlRootAttribute
                {
                    ElementName = XmiHelper.XmiStructure.DEFAULT_VALUE,
                    IsNullable = true,
                    Namespace = ""
                };

                using var xReader = new XmlNodeReader(DefaultValueElement);

                XmlSerializer? serial = null;
                string umlType = DefaultValueElement.GetAttribute(XmiHelper.XmiStructure.type, XmiHelper.XmiNamespace);
                switch (umlType)
                {
                    case XmiHelper.UmlStructure.InstanceValue:
                        serial = new XmlSerializer(typeof(UmlInstanceValue), xRoot);
                        break;
                    case XmiHelper.UmlStructure.LiteralString:
                        serial = new XmlSerializer(typeof(UmlLiteralString), xRoot);
                        break;
                    case XmiHelper.UmlStructure.LiteralInteger:
                        serial = new XmlSerializer(typeof(UmlLiteralInteger), xRoot);
                        break;
                    case XmiHelper.UmlStructure.LiteralReal:
                        serial = new XmlSerializer(typeof(UmlLiteralReal), xRoot);
                        break;
                    case XmiHelper.UmlStructure.LiteralBoolean:
                        serial = new XmlSerializer(typeof(UmlLiteralBoolean), xRoot);
                        break;
                    default:
                        break;
                }

                if (serial != null)
                {
                    object? deserializedObject = serial.Deserialize(xReader);

                    if (deserializedObject != null)
                    {
                        switch (umlType)
                        {
                            case XmiHelper.UmlStructure.InstanceValue:
                                _defaultValue = deserializedObject as UmlInstanceValue;
                                break;
                            case XmiHelper.UmlStructure.LiteralString:
                                _defaultValue = deserializedObject as UmlLiteralString;
                                break;
                            case XmiHelper.UmlStructure.LiteralInteger:
                                _defaultValue = deserializedObject as UmlLiteralInteger;
                                break;
                            case XmiHelper.UmlStructure.LiteralReal:
                                _defaultValue = deserializedObject as UmlLiteralReal;
                                break;
                            case XmiHelper.UmlStructure.LiteralBoolean:
                                _defaultValue = deserializedObject as UmlLiteralBoolean;
                                break;
                            default:
                                break;
                        }
                    }
                }

                return _defaultValue;
            }
        }

        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.XmiExtension"/> — one
        /// or more MagicDraw <c>xmi:Extension</c> blocks carrying tool-model
        /// metadata (multiplicity limits, tag applications, stereotype
        /// bindings). Vendored from mtconnect/MtconnectTranspiler v2.8
        /// where the equivalent shape is <c>XmiExtension[] Extensions</c>;
        /// the fork previously exposed only a single <c>Extension</c> and
        /// dropped every additional block silently. Widened here to array
        /// so all blocks are preserved.
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.EXTENSION, Namespace = XmiHelper.XmiNamespace)]
        public XmiExtension[]? Extensions { get; set; }

        /// <summary>
        /// Legacy compatibility surface that returns the first entry in
        /// <see cref="Extensions"/>, or <c>null</c> when the array is
        /// empty. Marked obsolete because MagicDraw commonly emits more
        /// than one <c>xmi:Extension</c> block per property; new callers
        /// should iterate <see cref="Extensions"/> directly.
        /// </summary>
        [XmlIgnore]
        [System.Obsolete("Use Extensions[] — a UmlProperty commonly carries more than one xmi:Extension block. This single-item accessor returns Extensions[0] and will be removed in a subsequent release.")]
        public XmiExtension? Extension => Extensions is { Length: > 0 } ? Extensions[0] : null;

        /// <summary>
        /// <c>visibility</c> attribute
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.visibility, Namespace = "")]
        public string Visibility { get; set; } = "public";

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.UML.UmlComment"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.OWNED_COMMENT, Namespace = "")]
        public UmlComment[]? Comments { get; set; }
    }
}