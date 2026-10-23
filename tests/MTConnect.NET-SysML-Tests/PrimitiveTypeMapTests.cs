using System.IO;
using System.Xml.Serialization;
using MTConnect.SysML;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML
{
    /// <summary>
    /// Coverage on <see cref="PrimitiveTypeMap"/> — the test-visible seam
    /// over the SysML primitive-to-C# type mappings extracted from the
    /// inline switch statements previously in
    /// <c>MTConnectPropertyModel.ParseType</c> and
    /// <c>CSharpTemplateRenderer.Render</c>. Verifies both parity with
    /// the pre-extraction tables and the vendored MtconnectTranspiler
    /// v2.8 extensions (<c>int32</c>, <c>int64</c>, <c>uint32</c>,
    /// <c>uint64</c>, <c>double</c>, <c>version</c>, <c>binary</c>,
    /// <c>UUID</c>, <c>float3d</c>) with per-primitive XMI fixtures that
    /// exercise the deserialisation path a real property would take,
    /// and asserts the corresponding default-value literal each
    /// primitive should emit.
    /// </summary>
    [TestFixture]
    public class PrimitiveTypeMapTests
    {
        // ---- Null / empty guards ----

        [TestCase(null)]
        [TestCase("")]
        public void MapByPropertyId_null_or_empty_returns_null(string? propertyId)
        {
            Assert.That(PrimitiveTypeMap.MapByPropertyId(propertyId), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MapByTypeId_null_or_empty_returns_null(string? typeId)
        {
            Assert.That(PrimitiveTypeMap.MapByTypeId(typeId), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MapByTypeName_null_or_empty_returns_null(string? typeName)
        {
            Assert.That(PrimitiveTypeMap.MapByTypeName(typeName), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        public void DefaultValueLiteral_null_or_empty_returns_null(string? csharpType)
        {
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharpType), Is.Null);
        }

        [Test]
        public void MapByPropertyId_unknown_returns_null()
        {
            Assert.That(PrimitiveTypeMap.MapByPropertyId("not-a-real-property-id"), Is.Null);
        }

        [Test]
        public void MapByTypeId_unknown_returns_null()
        {
            Assert.That(PrimitiveTypeMap.MapByTypeId("not-a-real-type-id"), Is.Null);
        }

        [Test]
        public void MapByTypeName_unknown_returns_null()
        {
            Assert.That(PrimitiveTypeMap.MapByTypeName("NotARealName"), Is.Null);
        }

        [Test]
        public void DefaultValueLiteral_unknown_returns_null()
        {
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral("NotARealType"), Is.Null);
        }

        // ---- Per-property override parity ----

        [TestCase("_19_0_3_68e0225_1618831247227_54016_392", "double",
            TestName = "RawMaterials_RawMateral_CurrentVolume_forces_double")]
        [TestCase("_19_0_3_68e0225_1622116618964_666287_1642", "MILLIMETER_3D",
            TestName = "RawMaterials_RawMateral_CurrentDimension_forces_MILLIMETER_3D")]
        [TestCase("_19_0_3_68e0225_1618831175692_489264_387", "double",
            TestName = "RawMaterials_RawMateral_InitialVolume_forces_double")]
        [TestCase("_19_0_3_68e0225_1622116618960_627070_1641", "MILLIMETER_3D",
            TestName = "RawMaterials_RawMateral_InitialDimension_forces_MILLIMETER_3D")]
        [TestCase("_19_0_3_68e0225_1636117526335_679126_67", "double",
            TestName = "CuttingTools_ProcessFeedRate_Value_forces_double")]
        public void MapByPropertyId_forces_the_expected_C_sharp_type(string propertyId, string expected)
        {
            Assert.That(PrimitiveTypeMap.MapByPropertyId(propertyId), Is.EqualTo(expected));
        }

        // ---- Historical UML primitive-id parity ----

        [TestCase("_19_0_3_91b028d_1579272360416_763325_681", "string", TestName = "UmlString_maps_to_string")]
        [TestCase("_19_0_3_91b028d_1579272271512_537408_674", "int", TestName = "UmlInteger_maps_to_int")]
        [TestCase("_19_0_3_91b028d_1579278876899_683310_3821", "bool", TestName = "UmlBoolean_maps_to_bool")]
        [TestCase("_19_0_3_91b028d_1579272506322_914606_702", "double", TestName = "UmlFloat_maps_to_double")]
        [TestCase("_19_0_3_68e0225_1678197512818_76309_18111", "double", TestName = "UmlDouble_maps_to_double")]
        [TestCase("_19_0_3_91b028d_1579272233011_597138_670", "System.DateTime", TestName = "UmlDateTime_maps_to_System_DateTime")]
        [TestCase("EAID_64352755_7251_46af_846D_937E5A1E3949", "Description", TestName = "UmlDescription_maps_to_Description")]
        [TestCase("_19_0_3_91b028d_1579272245466_691733_672", "string", TestName = "UmlId_maps_to_string")]
        [TestCase("_19_0_3_45f01b9_1579563576485_587701_22033", "string", TestName = "DataItemTypeEnum_maps_to_string")]
        [TestCase("_19_0_3_45f01b9_1579563592155_977172_22064", "string", TestName = "DataItemSubTypeEnum_maps_to_string")]
        public void MapByTypeId_returns_the_expected_C_sharp_type(string typeId, string expected)
        {
            Assert.That(PrimitiveTypeMap.MapByTypeId(typeId), Is.EqualTo(expected));
        }

        // ---- Named-type parity (existing renderer coercions) ----

        [TestCase("UnitsEnum", "string")]
        [TestCase("NativeUnitsEnum", "string")]
        [TestCase("MeasurementCodeEnum", "string")]
        [TestCase("UNIT_VECTOR_3D", "MTConnect.UnitVector3D")]
        [TestCase("POSITION_3D", "MTConnect.Position3D")]
        [TestCase("DEGREE_3D", "MTConnect.Degree3D")]
        [TestCase("MILLIMETER_3D", "MTConnect.Millimeter3D")]
        [TestCase("QIFDocument", "string")]
        public void MapByTypeName_pre_vendor_coercions_are_preserved(string typeName, string expected)
        {
            Assert.That(PrimitiveTypeMap.MapByTypeName(typeName), Is.EqualTo(expected));
        }

        // ---- Vendored MtconnectTranspiler v2.8 primitives (name → C# type +
        //      default-value literal), one XMI fixture per primitive ----

        // Each per-primitive test round-trips through
        //   1. an XMI fixture declaring `<ownedAttribute type='xxx'>` — the
        //      shape the property carries after parse, i.e. `property.DataType
        //      == "xxx"`;
        //   2. the seam under test — MapByTypeName; and
        //   3. the default-value literal for the mapped C# type.

        [Test]
        public void Int32_maps_to_int_and_defaults_to_zero()
        {
            var xmi = MakeUmlPropertyXmi(type: "int32");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("int32"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("int"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("0"));
        }

        [Test]
        public void Int64_maps_to_long_and_defaults_to_zero_long()
        {
            var xmi = MakeUmlPropertyXmi(type: "int64");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("int64"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("long"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("0L"));
        }

        [Test]
        public void UInt32_maps_to_uint_and_defaults_to_zero_uint()
        {
            var xmi = MakeUmlPropertyXmi(type: "uint32");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("uint32"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("uint"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("0u"));
        }

        [Test]
        public void UInt64_maps_to_ulong_and_defaults_to_zero_ulong()
        {
            var xmi = MakeUmlPropertyXmi(type: "uint64");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("uint64"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("ulong"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("0uL"));
        }

        [Test]
        public void Double_named_maps_to_double_and_defaults_to_zero_point_zero()
        {
            var xmi = MakeUmlPropertyXmi(type: "double");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("double"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("double"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("0.0"));
        }

        [Test]
        public void Version_maps_to_System_Version_and_defaults_to_null()
        {
            var xmi = MakeUmlPropertyXmi(type: "version");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("version"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("System.Version"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("null"));
        }

        [Test]
        public void Binary_transforms_to_bool_and_defaults_to_false()
        {
            var xmi = MakeUmlPropertyXmi(type: "binary");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("binary"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("bool"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("false"));
        }

        [Test]
        public void UUID_transforms_to_string_and_defaults_to_null()
        {
            var xmi = MakeUmlPropertyXmi(type: "UUID");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("UUID"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("string"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("null"));
        }

        [Test]
        public void Float3d_transforms_to_float_array_and_defaults_to_null()
        {
            var xmi = MakeUmlPropertyXmi(type: "float3d");
            var property = DeserializeUmlProperty(xmi);
            Assert.That(property.PropertyType, Is.EqualTo("float3d"));

            var csharp = PrimitiveTypeMap.MapByTypeName(property.PropertyType);
            Assert.That(csharp, Is.EqualTo("float[]"));
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharp!), Is.EqualTo("null"));
        }

        // ---- Default-value coverage for the historical types ----

        [TestCase("string", "null")]
        [TestCase("int", "0")]
        [TestCase("bool", "false")]
        [TestCase("float", "0.0f")]
        [TestCase("System.DateTime", "default(System.DateTime)")]
        [TestCase("MTConnect.UnitVector3D", "default(MTConnect.UnitVector3D)")]
        [TestCase("MTConnect.Position3D", "default(MTConnect.Position3D)")]
        [TestCase("MTConnect.Degree3D", "default(MTConnect.Degree3D)")]
        [TestCase("MTConnect.Millimeter3D", "default(MTConnect.Millimeter3D)")]
        [TestCase("Description", "null")]
        public void DefaultValueLiteral_returns_the_expected_source_literal(string csharpType, string expected)
        {
            Assert.That(PrimitiveTypeMap.DefaultValueLiteral(csharpType), Is.EqualTo(expected));
        }

        // ---- Precedence between the property-id override and the type-id map ----
        //
        // The overrides in `_byPropertyId` are consulted BEFORE the type-id
        // lookup in `MTConnectPropertyModel.ParseType`, so a property-id hit
        // wins over whatever the declared XMI type would otherwise yield.
        // The unit-level assertion is trivial — the two APIs are independent
        // on this helper — but the precedence is called out here so the
        // renderer never silently regresses to the type-id-first order.

        [Test]
        public void MapByPropertyId_and_MapByTypeId_are_independent_lookups()
        {
            // A property with an override that resolves to "double" whose
            // declared type is UML integer (which would otherwise map to
            // "int"). The two helpers stay independent — the parser
            // (MTConnectPropertyModel.ParseType) applies them in the
            // documented order.
            var overrideResult = PrimitiveTypeMap.MapByPropertyId("_19_0_3_68e0225_1618831247227_54016_392");
            var typeResult = PrimitiveTypeMap.MapByTypeId("_19_0_3_91b028d_1579272271512_537408_674");

            Assert.That(overrideResult, Is.EqualTo("double"));
            Assert.That(typeResult, Is.EqualTo("int"));
        }

        // ---- Helpers ----

        // Builds an XMI fragment for a single `<ownedAttribute xmi:type='uml:Property'>`
        // element with the requested declared type. The xmlns declaration on
        // the element itself is required so the `xmi:` prefix resolves — the
        // pattern matches the fixture shape used by
        // Xmi/UML/UmlPropertyWideningTests.cs.
        private static string MakeUmlPropertyXmi(string type)
        {
            return $@"<ownedAttribute xmi:type='uml:Property' xmi:id='p1' type='{type}'
    xmlns:xmi='http://www.omg.org/spec/XMI/20131001' />";
        }

        private static UmlProperty DeserializeUmlProperty(string xml)
        {
            var serializer = new XmlSerializer(typeof(UmlProperty));
            using var reader = new StringReader(xml);
            return (UmlProperty)serializer.Deserialize(reader)!;
        }
    }
}
