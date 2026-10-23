using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.UML;
using System.Linq;

namespace MTConnect.SysML
{
    /// <summary>
    /// A parsed class property: its emitted name and C# data type, whether
    /// it is optional (nullable) or a collection, and its cleaned
    /// description.
    /// </summary>
    public class MTConnectPropertyModel : IMTConnectExportModel
    {
        /// <inheritdoc/>
        public string UmlId { get; set; }

        /// <inheritdoc/>
        public string Id { get; set; }

        /// <summary>
        /// The property name as emitted in the generated C#.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The cleaned description text emitted into the doc comment,
        /// falling back to the description of the property's type when the
        /// property itself has none.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The resolved C# data type of the property.
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// The <c>xmi:id</c> of the property's declared type.
        /// </summary>
        public string DataTypeUmlId { get; set; }

        /// <summary>
        /// True when the property is optional and must be emitted as a
        /// nullable member.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// True when the property is a collection and must be emitted as an
        /// enumerable member.
        /// </summary>
        public bool IsArray { get; set; }

        /// <summary>
        /// True when the property's <see cref="Name"/> hides an inherited member on
        /// the generated <em>class</em> declaration. Model.scriban and the
        /// DataSetResults template emit a <c>new</c> modifier on the property when
        /// this is set, suppressing CS0108 ("hides inherited member; use the new
        /// keyword if hiding was intended"). Populated by the per-renderer
        /// inheritance pass — the SysML-declared parent chain plus hand-stitched
        /// seeds for class-side inheritance links the SysML model does not express
        /// (the hand-written <c>Observation</c> base of every DataSetResult).
        /// </summary>
        public bool IsInherited { get; set; }

        /// <summary>
        /// True when the property's <see cref="Name"/> hides an inherited member on
        /// the generated <em>interface</em> declaration. Interface.scriban emits a
        /// <c>new</c> modifier on the property when this is set, suppressing the
        /// same CS0108. Separate from <see cref="IsInherited"/> because the
        /// inheritance picture can diverge between the class and interface sides —
        /// e.g. <c>IComposition</c>'s hand-written partial extends <c>IContainer</c>
        /// (interface hides <c>Type</c>) but <c>Composition</c>'s hand-written
        /// partial does not extend <c>Container</c> as a class base (the class does
        /// not hide). The renderer's inheritance walk seeds both flags from the
        /// SysML chain and adds interface-only seeds where the hand-written
        /// interface partial extends a base the class does not.
        /// </summary>
        public bool IsInheritedInInterface { get; set; }


        /// <summary>
        /// Creates an empty model for manual population.
        /// </summary>
        public MTConnectPropertyModel() { }

        /// <summary>
        /// Parses a property from <paramref name="umlProperty"/> under
        /// <paramref name="idPrefix"/>: normalizes the name (strips a
        /// leading <c>has</c>, maps <c>xlink:type</c>, pluralizes
        /// collections), resolves the C# data type, and cleans the
        /// description.
        /// </summary>
        public MTConnectPropertyModel(XmiDocument xmiDocument, string idPrefix, UmlProperty umlProperty)
        {
            UmlId = umlProperty.Id;

            if (xmiDocument != null && umlProperty != null)
            {
                IsArray = ModelHelper.IsArray(xmiDocument, umlProperty.Id);
                IsOptional = ModelHelper.IsOptional(xmiDocument, umlProperty.Id);

                var propertyName = umlProperty.Name;
                if (propertyName.StartsWith("has") && propertyName != "hash") propertyName = propertyName.Substring(3);

                if (propertyName == "xlink:type") propertyName = "xLinkType";

                var name = propertyName.ToTitleCase();
                if (IsArray) name = ModelHelper.ConvertArrayName(name);

                Id = $"{idPrefix}.{name}";
                Name = name;
                DataType = ParseType(xmiDocument, umlProperty.Id, umlProperty.PropertyType);
                DataTypeUmlId = umlProperty.PropertyType;

                var description = umlProperty.Comments?.FirstOrDefault().Body;
                Description = ModelHelper.ProcessDescription(description);
                if (string.IsNullOrEmpty(Description)) Description = ModelHelper.GetClassDescription(xmiDocument, umlProperty.PropertyType);
            }
        }

        /// <summary>
        /// Resolves the C# data type for a property: applies the
        /// per-property and per-type <c>xmi:id</c> overrides for the cases
        /// the XMI types incorrectly, then maps primitive types, value
        /// classes, and enumerations, defaulting to <c>string</c>.
        /// The per-property and per-type-id override tables live in
        /// <see cref="PrimitiveTypeMap"/> — extracted from the inline
        /// switch statements that used to sit here so the tables are
        /// reachable from the tests (and shared with the C# template
        /// renderer's named-type switch, which also delegates through
        /// the same helper).
        /// </summary>
        internal static string ParseType(XmiDocument xmiDocument, string propertyId, string typeId)
        {
            if (xmiDocument != null && propertyId != null && typeId != null)
            {
                var propertyOverride = PrimitiveTypeMap.MapByPropertyId(propertyId);
                if (propertyOverride != null) return propertyOverride;

                var primitive = PrimitiveTypeMap.MapByTypeId(typeId);
                if (primitive != null) return primitive;

                string dataType = null;

                var dataClass = ModelHelper.GetClass(xmiDocument, typeId);
                if (dataClass != null)
                {
                    if (ModelHelper.IsValueClass(dataClass))
                    {
                        dataType = ModelHelper.GetValueType(xmiDocument, dataClass);
                    }
                    else
                    {
                        dataType = dataClass.Name;
                    }
                }


                //var dataType = ModelHelper.GetClassName(xmiDocument, typeId);
                if (string.IsNullOrEmpty(dataType)) dataType = ModelHelper.GetEnumName(xmiDocument, typeId);
                if (string.IsNullOrEmpty(dataType)) dataType = "string";
                return dataType;
            }

            return null;
        }
    }
}
