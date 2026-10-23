// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed under MTConnect.SysML.Xmi.UML to fold into the
// fork's existing XMI tree. See UmlLiteralInteger.cs for the adaptation
// rationale.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.UML
{
    /// <summary>
    /// <inheritdoc cref="MTConnect.SysML.Xmi.DefaultValue"/> where
    /// <c>xmi:type='uml:LiteralBoolean'</c>. Materialises boolean
    /// defaults for flag-like SysML properties (deprecation markers,
    /// nullability toggles). The wire format is UML-standard lowercase
    /// <c>true</c> / <c>false</c>.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.XmiStructure.DEFAULT_VALUE, Namespace = "")]
    public class UmlLiteralBoolean : DefaultValue
    {
        /// <inheritdoc cref="MTConnect.SysML.Xmi.XmiElement.Type"/>
        public override string Type => XmiHelper.UmlStructure.LiteralBoolean;

        /// <summary>
        /// <c>value</c> attribute. Nullable so an absent attribute
        /// round-trips as <c>null</c> rather than <c>false</c>.
        /// </summary>
        [XmlIgnore]
        public bool? Value { get; set; }

        /// <summary>
        /// Serializer-facing string surface for <see cref="Value"/>.
        /// Emits UML-canonical lowercase <c>true</c> / <c>false</c>; a
        /// culture-invariant TitleCase would be rejected by strict
        /// UML validators.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.value, Namespace = "")]
        public string? ValueSerializable
        {
            get => Value?.ToString().ToLowerInvariant();
            set
            {
                if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var result))
                    Value = result;
                else
                    Value = null;
            }
        }
    }
}
