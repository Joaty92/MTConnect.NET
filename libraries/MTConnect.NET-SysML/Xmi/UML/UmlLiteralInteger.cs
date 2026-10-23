// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed under MTConnect.SysML.Xmi.UML to fold into the
// fork's existing XMI tree — folded, not siblinged. Adaptations from
// upstream:
//   - MtconnectTranspiler.Xmi.UML => MTConnect.SysML.Xmi.UML
//   - XmlHelper.* => XmiHelper.* (the fork's internal helper class name)
//   - constant names unchanged so the wire format matches upstream
using System;
using System.Globalization;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.UML
{
    /// <summary>
    /// <inheritdoc cref="MTConnect.SysML.Xmi.DefaultValue"/> where
    /// <c>xmi:type='uml:LiteralInteger'</c>. A default-value variant introduced
    /// alongside the three MTConnect Institute additions vendored from upstream
    /// v2.8, integrated directly into the fork's DefaultValue hierarchy so the
    /// <see cref="UmlProperty.DefaultValue"/> switch can materialise integer
    /// defaults (multiplicity bounds, deprecation-version markers, etc.) rather
    /// than falling through to <c>null</c>.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.XmiStructure.DEFAULT_VALUE, Namespace = "")]
    public class UmlLiteralInteger : DefaultValue
    {
        /// <inheritdoc cref="MTConnect.SysML.Xmi.XmiElement.Type"/>
        public override string Type => XmiHelper.UmlStructure.LiteralInteger;

        /// <summary>
        /// <c>value</c> attribute. Nullable so an absent attribute round-trips
        /// as <c>null</c> rather than <c>0</c>.
        /// </summary>
        [XmlIgnore]
        public int? Value { get; set; }

        /// <summary>
        /// Serializer-facing string surface for <see cref="Value"/>. Parsing
        /// uses invariant culture so an XMI produced in a locale that uses a
        /// comma decimal separator still round-trips.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.value, Namespace = "")]
        public string? ValueSerializable
        {
            get => Value?.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                    Value = result;
                else
                    Value = null;
            }
        }
    }
}
