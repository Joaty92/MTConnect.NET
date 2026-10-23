// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed under MTConnect.SysML.Xmi.UML to fold into the
// fork's existing XMI tree. See UmlLiteralInteger.cs for the adaptation
// rationale.
using System;
using System.Globalization;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.UML
{
    /// <summary>
    /// <inheritdoc cref="MTConnect.SysML.Xmi.DefaultValue"/> where
    /// <c>xmi:type='uml:LiteralReal'</c>. Materialises real-number
    /// defaults for properties whose SysML default is a floating point
    /// literal — e.g. thresholds on <c>Constraint</c>, or numeric
    /// initialisers on <c>ValueType</c> properties.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.XmiStructure.DEFAULT_VALUE, Namespace = "")]
    public class UmlLiteralReal : DefaultValue
    {
        /// <inheritdoc cref="MTConnect.SysML.Xmi.XmiElement.Type"/>
        public override string Type => XmiHelper.UmlStructure.LiteralReal;

        /// <summary>
        /// <c>value</c> attribute. Backed by <c>double</c> for wider range
        /// than the upstream <c>float?</c> shape — MTConnect SysML
        /// unbounded upper-bounds (<c>*</c>) that the parser upgrades to
        /// <see cref="double.PositiveInfinity"/> would truncate through
        /// float. Nullable so an absent attribute round-trips as
        /// <c>null</c> rather than <c>0.0</c>.
        /// </summary>
        [XmlIgnore]
        public double? Value { get; set; }

        /// <summary>
        /// Serializer-facing string surface for <see cref="Value"/>.
        /// Parsing uses invariant culture so an XMI produced in a locale
        /// that uses a comma decimal separator still round-trips.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.value, Namespace = "")]
        public string? ValueSerializable
        {
            get => Value?.ToString("R", CultureInfo.InvariantCulture);
            set
            {
                if (!string.IsNullOrEmpty(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                    Value = result;
                else
                    Value = null;
            }
        }
    }
}
