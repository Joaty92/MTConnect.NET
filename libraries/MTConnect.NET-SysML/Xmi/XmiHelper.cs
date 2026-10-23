namespace MTConnect.SysML.Xmi
{
    internal static class XmiHelper
    {
        public const string XmiNamespace = "http://www.omg.org/spec/XMI/20131001";
        public const string UmlNamespace = "http://www.omg.org/spec/UML/20131001";
        public const string ProfileNamespace = "http://www.magicdraw.com/schemas/Profile.xmi";
        public const string StandardProfileNamespace = "http://www.omg.org/spec/UML/20131001/StandardProfile";
        public const string Validation_ProfileNamespace = "http://www.magicdraw.com/schemas/Validation_Profile.xmi";
        public const string Dependency_Matrix_ProfileNamespace = "http://www.magicdraw.com/schemas/Dependency_Matrix_Profile.xmi";
        public const string Concept_Modeling_ProfileNamespace = "http://www.magicdraw.com/schemas/Concept_Modeling_Profile.xmi";
        public const string DSL_CustomizationNamespace = "http://www.magicdraw.com/schemas/DSL_Customization.xmi";
        public const string SysMlNamespace = "http://www.omg.org/spec/SysML/20150709/SysML";
        public const string MagicDraw_ProfileNamespace = "http://www.omg.org/spec/UML/20131001/MagicDrawProfile";
        public const string Ccm_Internal_Implementation_ProfileNamespace = "http://www.magicdraw.com/schemas/CCM_Internal_Implementation_Profile.xmi";
        public const string Md_Customization_for_SysML__additional_stereotypesNamespace = "http://www.magicdraw.com/spec/Customization/180/SysML";
        public const string SimulationProfileNamespace = "http://www.magicdraw.com/schemas/SimulationProfile.xmi";

        /// <summary>
        /// XML tag / attribute constants for the
        /// <c>Concept_Modeling_Profile</c> MagicDraw profile. Vendored from
        /// mtconnect/MtconnectTranspiler v2.8 alongside the
        /// <see cref="MTConnect.SysML.Xmi.ConceptModelingProfile"/> class
        /// tree so the fork can round-trip Anything / Resource / Restriction
        /// / Functional / Transitive / DisjointWith / EquivalentClass /
        /// LiteralAnnotation stereotype applications.
        /// </summary>
        public static class ConceptModelingProfileStructure
        {
            #region XML Tags
            /// <summary><c>&lt;Concept_Modeling_Profile:Literal_Annotation /&gt;</c></summary>
            public const string LITERAL_ANNOTATION = "Literal_Annotation";
            /// <summary><c>&lt;Concept_Modeling_Profile:Restriction /&gt;</c></summary>
            public const string RESTRICTION = "Restriction";
            /// <summary><c>&lt;Concept_Modeling_Profile:Equivalent_Class /&gt;</c></summary>
            public const string EQUIVALENT_CLASS = "Equivalent_Class";
            /// <summary><c>&lt;Concept_Modeling_Profile:Functional /&gt;</c></summary>
            public const string FUNCTIONAL = "Functional";
            /// <summary><c>&lt;Concept_Modeling_Profile:Resource /&gt;</c></summary>
            public const string RESOURCE = "Resource";
            /// <summary><c>&lt;Concept_Modeling_Profile:Transitive /&gt;</c></summary>
            public const string TRANSITIVE = "Transitive";
            /// <summary><c>&lt;Concept_Modeling_Profile:Disjoint_With /&gt;</c></summary>
            public const string DISJOINT_WITH = "Disjoint_With";
            /// <summary><c>&lt;Concept_Modeling_Profile:Anything /&gt;</c></summary>
            public const string ANYTHING = "Anything";
            #endregion

            #region XML Attributes
            /// <summary><c>base_Comment</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string baseComment = "base_Comment";
            /// <summary><c>base_Class</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string baseClass = "base_Class";
            /// <summary><c>base_Property</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string baseProperty = "base_Property";
            /// <summary><c>base_Generalization</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string baseGeneralization = "base_Generalization";
            /// <summary><c>IRI</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string IRI = "IRI";
            /// <summary><c>base_Dependency</c> attribute on a Concept_Modeling_Profile element.</summary>
            public const string baseDependency = "base_Dependency";
            #endregion
        }

        /// <summary>
        /// XML tag / attribute constants for the <c>MagicDraw_Profile</c>
        /// profile. Vendored from mtconnect/MtconnectTranspiler v2.8
        /// alongside the <see cref="MTConnect.SysML.Xmi.MagicDrawProfile"/>
        /// class tree so the fork can round-trip
        /// additionalElementImport / additionalPackageImport / DiagramInfo /
        /// DiagramTable / InstanceTable / CustomSort stereotype
        /// applications.
        /// </summary>
        public static class MagicDrawProfileStructure
        {
            #region XML Tags
            /// <summary><c>&lt;MagicDraw_Profile:additionalElementImport /&gt;</c></summary>
            public const string ADDITIONAL_ELEMENT_IMPORT = "additionalElementImport";
            /// <summary><c>&lt;MagicDraw_Profile:additionalPackageImport /&gt;</c></summary>
            public const string ADDITIONAL_PACKAGE_IMPORT = "additionalPackageImport";
            /// <summary><c>&lt;MagicDraw_Profile:DiagramInfo /&gt;</c></summary>
            public const string DIAGRAM_INFO = "DiagramInfo";
            /// <summary><c>&lt;MagicDraw_Profile:DiagramTable /&gt;</c></summary>
            public const string DIAGRAM_TABLE = "DiagramTable";
            /// <summary><c>&lt;MagicDraw_Profile:InstanceTable /&gt;</c></summary>
            public const string INSTANCE_TABLE = "InstanceTable";
            /// <summary><c>&lt;MagicDraw_Profile:CustomSort /&gt;</c></summary>
            public const string CUSTOM_SORT = "CustomSort";
            /// <summary><c>&lt;rowElements /&gt;</c> nested child.</summary>
            public const string ROW_ELEMENTS = "rowElements";
            /// <summary><c>&lt;hideColumns /&gt;</c> nested child.</summary>
            public const string HIDE_COLUMNS = "hideColumns";
            /// <summary><c>&lt;expandedRows /&gt;</c> nested child.</summary>
            public const string EXPANDED_ROWS = "expandedRows";
            /// <summary><c>&lt;sort /&gt;</c> nested child.</summary>
            public const string SORT = "sort";
            /// <summary><c>&lt;columnIds /&gt;</c> nested child.</summary>
            public const string COLUMN_IDS = "columnIds";
            /// <summary><c>&lt;columnWidth /&gt;</c> nested child.</summary>
            public const string COLUMN_WIDTH = "columnWidth";
            /// <summary><c>&lt;customColumns /&gt;</c> nested child.</summary>
            public const string CUSTOM_COLUMNS = "customColumns";
            #endregion

            #region XML Attributes
            /// <summary><c>base_ElementImport</c> attribute.</summary>
            public const string baseElementImport = "base_ElementImport";
            /// <summary><c>base_PackageImport</c> attribute.</summary>
            public const string basePackageImport = "base_PackageImport";
            /// <summary><c>treatAsAuxiliaryInOwningProject</c> attribute.</summary>
            public const string treatAsAuxiliaryInOwningProject = "treatAsAuxiliaryInOwningProject";
            /// <summary><c>base_Diagram</c> attribute.</summary>
            public const string baseDiagram = "base_Diagram";
            /// <summary><c>Author</c> attribute.</summary>
            public const string author = "Author";
            /// <summary><c>Creation_date</c> attribute.</summary>
            public const string creationDate = "Creation_date";
            /// <summary><c>classifiers</c> attribute.</summary>
            public const string classifiers = "classifiers";
            /// <summary><c>scope</c> attribute.</summary>
            public const string scope = "scope";
            /// <summary><c>includeSubtypesOfRowTypes</c> attribute.</summary>
            public const string includeSubtypesOfRowTypes = "includeSubtypesOfRowTypes";
            /// <summary><c>showUnitsOnValues</c> attribute.</summary>
            public const string showUnitsOnValues = "showUnitsOnValues";
            /// <summary><c>rowsOrder</c> attribute.</summary>
            public const string rowsOrder = "rowsOrder";
            /// <summary><c>includeCustomTypesOfRowTypes</c> attribute.</summary>
            public const string includeCustomTypesOfRowTypes = "includeCustomTypesOfRowTypes";
            /// <summary><c>Modification_date</c> attribute.</summary>
            public const string modificationDate = "Modification_date";
            /// <summary><c>Last_modified_by</c> attribute.</summary>
            public const string lastModifiedBy = "Last_modified_by";
            /// <summary><c>base_Element</c> attribute.</summary>
            public const string baseElement = "base_Element";
            /// <summary><c>sortPriority</c> attribute.</summary>
            public const string sortPriority = "sortPriority";
            /// <summary><c>showDetailedColumnName</c> attribute.</summary>
            public const string showDetailedColumnName = "showDetailedColumnName";
            /// <summary><c>typesIncludeSubtypes</c> attribute.</summary>
            public const string typesIncludeSubtypes = "typesIncludeSubtypes";
            /// <summary><c>displayMode</c> attribute.</summary>
            public const string displayMode = "displayMode";
            /// <summary><c>showElementNumber</c> attribute.</summary>
            public const string showElementNumber = "showElementNumber";
            /// <summary><c>showColumnIcons</c> attribute.</summary>
            public const string showColumnIcons = "showColumnIcons";
            /// <summary><c>showScopeAsRoot</c> attribute.</summary>
            public const string showScopeAsRoot = "showScopeAsRoot";
            /// <summary><c>showScope</c> attribute.</summary>
            public const string showScope = "showScope";
            /// <summary><c>showFilter</c> attribute.</summary>
            public const string showFilter = "showFilter";
            /// <summary><c>showElementType</c> attribute.</summary>
            public const string showElementType = "showElementType";
            /// <summary><c>additionalElements</c> attribute.</summary>
            public const string additionalElements = "additionalElements";
            #endregion
        }

        /// <summary>
        /// XML tag / attribute constants for the
        /// <c>MD_Customization_for_SysML__additional_stereotypes</c>
        /// customization profile. Vendored from mtconnect/MtconnectTranspiler
        /// v2.8 alongside the
        /// <see cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes"/>
        /// class tree so the fork can round-trip ValueProperty /
        /// PartProperty / ReferenceProperty / ConstraintProperty /
        /// ConstraintParameter / ExternalModel stereotype applications on
        /// SysML block features.
        /// </summary>
        public static class MDCustomizationForSysMLAdditionalStereoTypes
        {
            #region XML Tags
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:ValueProperty /&gt;</c></summary>
            public const string VALUE_PROPERTY = "ValueProperty";
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:PartProperty /&gt;</c></summary>
            public const string PART_PROPERTY = "PartProperty";
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:ReferenceProperty /&gt;</c></summary>
            public const string REFERENCE_PROPERTY = "ReferenceProperty";
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:ConstraintProperty /&gt;</c></summary>
            public const string CONSTRAINT_PROPERTY = "ConstraintProperty";
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:ConstraintParameter /&gt;</c></summary>
            public const string CONSTRAINT_PARAMETER = "ConstraintParameter";
            /// <summary><c>&lt;MD_Customization_for_SysML__additional_stereotypes:ExternalModel /&gt;</c></summary>
            public const string EXTERNAL_MODEL = "ExternalModel";
            #endregion

            #region XML Attributes
            /// <summary><c>base_Property</c> attribute.</summary>
            public const string baseProperty = "base_Property";
            /// <summary><c>base_Port</c> attribute.</summary>
            public const string basePort = "base_Port";
            /// <summary><c>base_Element</c> attribute.</summary>
            public const string baseElement = "base_Element";
            #endregion
        }

        public static class ProfileStructure
        {
            #region XML Tags
            public const string NORMATIVE = "normative";
            public const string DEPRECATED = "deprecated";
            public const string EXTENSIBLE = "extensible";
            public const string INFORMATIVE = "informative";
            public const string OBSERVES = "observes";
            public const string ORGANIZER = "organizer";
            public const string VALUE_TYPE = "valueType";
            /// <summary>
            /// <c>&lt;Profile:updated /&gt;</c> child element on a
            /// <see cref="MTConnect.SysML.Xmi.Profile.Normative"/> stereotype
            /// application, holding one MTConnect version at which the
            /// stereotyped element was updated. Vendored from mtconnect/
            /// MtconnectTranspiler v2.8 so the fork can preserve the full
            /// version history of every normative element.
            /// </summary>
            public const string UPDATED = "updated";
            #endregion
        }

        public static class XmiStructure
        {
            #region XML Tags
            public const string PACKAGED_ELEMENT = "packagedElement";
            public const string PACKAGE_IMPORT = "packageImport";
            public const string OWNED_COMMENT = "ownedComment";
            public const string OWNED_END = "ownedEnd";
            public const string OWNED_LITERAL = "ownedLiteral";
            public const string OWNED_RULE = "ownedRule";
            public const string OWNED_ATTRIBUTE = "ownedAttribute";
            public const string OWNED_OPERATION = "ownedOperation";
            public const string OWNED_PARAMETER = "ownedParameter";
            public const string BODY = "body";
            public const string SPECIFICATION = "specification";
            public const string LANGUAGE = "language";
            public const string GENERALIZATION = "generalization";
            public const string GENERAL = "general";
            public const string TYPE = "type";
            public const string ASSOCIATION = "association";
            public const string DEFAULT_VALUE = "defaultValue";
            public const string REDEFINED_PROPERTY = "redefinedProperty";
            public const string SUBSETTED_PROPERTY = "subsettedProperty";
            public const string MODEL = "Model";
            public const string ANNOTATED_ELEMENT = "annotatedElement";
            public const string LOWER_VALUE = "lowerValue";
            public const string UPPER_VALUE = "upperValue";
            public const string EXTENSION = "Extension";
            public const string MODEL_EXTENSION = "modelExtension";
            public const string CONSTRAINED_ELEMENT = "constrainedElement";
            public const string MEMBER_END = "memberEnd";
            public const string METAMODEL_REFERENCE = "metamodelReference";
            public const string DOCUMENTATION = "Documentation";
            public const string EXPORTER = "exporter";
            public const string EXPORTER_VERSION = "exporterVersion";
            #endregion

            #region XML Attributes
            public const string visibility = "visibility";
            public const string isQuery = "isQuery";
            public const string isAbstract = "isAbstract";
            public const string aggregation = "aggregation";
            public const string id = "id";
            public const string idRef = "idref";
            public const string type = "type";
            public const string name = "name";
            public const string version = "version";
            public const string introduced = "introduced";
            public const string baseElement = "base_Element";
            public const string baseClass = "base_Class";
            public const string baseComment = "base_Comment";
            public const string baseEnumeration = "base_Enumeration";
            public const string baseAssociation = "base_Association";
            public const string importedPackage = "importedPackage";
            public const string href = "href";
            public const string association = "association";
            public const string instance = "instance";
            public const string isStatic = "isStatic";
            public const string isReadOnly = "isReadOnly";
            public const string value = "value";
            public const string extender = "extender";
            #endregion
        }

        public class UmlStructure
        {
            #region UML xmi:type options
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Enumeration' /&gt;</c>
            /// </summary>
            public const string Enumeration = "uml:Enumeration";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:DataType' /&gt;</c>
            /// </summary>
            public const string DataType = "uml:DataType";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Class' /&gt;</c>
            /// </summary>
            public const string Class = "uml:Class";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Stereotype' /&gt;</c>
            /// </summary>
            public const string Stereotype = "uml:Stereotype";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Extension' /&gt;</c>
            /// </summary>
            public const string Extension = "uml:Extension";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Package' /&gt;</c>
            /// </summary>
            public const string Package = "uml:Package";
            /// <summary>
            /// <c>&lt;ownedComment xmi:type='uml:Comment' /&gt;</c>
            /// </summary>
            public const string Comment = "uml:Comment";
            /// <summary>
            /// <c>&lt;ownedRule xmi:type='uml:Constraint' /&gt;</c>
            /// </summary>
            public const string Constraint = "uml:Constraint";
            /// <summary>
            /// <c>&lt;ownedLiteral xmi:type='uml:EnumerationLiteral' /&gt;</c>
            /// </summary>
            public const string EnumerationLiteral = "uml:EnumerationLiteral";
            /// <summary>
            /// <c>&lt;ownedEnd xmi:type='uml:ExtensionEnd' /&gt;</c>
            /// </summary>
            public const string ExtensionEnd = "uml:ExtensionEnd";
            /// <summary>
            /// <c>&lt;generalization xmi:type='uml:Generalization' /&gt;</c>
            /// </summary>
            public const string Generalization = "uml:Generalization";
            /// <summary>
            /// <c>&lt;defaultValue xmi:type='uml:InstanceValue' /&gt;</c>
            /// </summary>
            public const string InstanceValue = "uml:InstanceValue";
            /// <summary>
            /// <c>&lt;defaultValue xmi:type='uml:LiteralString' /&gt;</c>
            /// </summary>
            public const string LiteralString = "uml:LiteralString";
            /// <summary>
            /// <c>&lt;defaultValue xmi:type='uml:LiteralInteger' /&gt;</c>.
            /// Vendored from mtconnect/MtconnectTranspiler v2.8 alongside
            /// <see cref="LiteralReal"/> + <see cref="LiteralBoolean"/> so the
            /// fork can parse integer default values (multiplicity bounds,
            /// deprecation-version integer literals).
            /// </summary>
            public const string LiteralInteger = "uml:LiteralInteger";
            /// <summary>
            /// <c>&lt;defaultValue xmi:type='uml:LiteralReal' /&gt;</c>.
            /// Vendored from mtconnect/MtconnectTranspiler v2.8.
            /// </summary>
            public const string LiteralReal = "uml:LiteralReal";
            /// <summary>
            /// <c>&lt;defaultValue xmi:type='uml:LiteralBoolean' /&gt;</c>.
            /// Vendored from mtconnect/MtconnectTranspiler v2.8.
            /// </summary>
            public const string LiteralBoolean = "uml:LiteralBoolean";
            /// <summary>
            /// <c>&lt;uml:Model xmi:type='uml:Model' /&gt;</c>
            /// </summary>
            public const string Model = "uml:Model";
            /// <summary>
            /// <c>&lt;specification xmi:type='uml:OpaqueExpression' /&gt;</c>
            /// </summary>
            public const string OpaqueExpression = "uml:OpaqueExpression";
            /// <summary>
            /// <c>&lt;packageImport xmi:type='uml:PackageImport' /&gt;</c>
            /// </summary>
            public const string PackageImport = "uml:PackageImport";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:PrimitiveType' /&gt;</c>
            /// </summary>
            public const string PrimitiveType = "uml:PrimitiveType";
            /// <summary>
            /// <c>&lt;packagedElement xmi:type='uml:Profile' /&gt;</c>
            /// </summary>
            public const string Profile = "uml:Profile";
            /// <summary>
            /// <c>&lt;ownedAttribute xmi:type='uml:Property' /&gt;</c>
            /// </summary>
            public const string Property = "uml:Property";
            /// <summary>
            /// <c>&lt;ownedAttribute xmi:type='uml:AssociationClass' /&gt;</c>
            /// </summary>
            public const string AssociationClass = "uml:AssociationClass";
            /// <summary>
            /// <c>&lt;ownedAttribute xmi:type='uml:Association' /&gt;</c>
            /// </summary>
            public const string Association = "uml:Association";
            #endregion
        }
    }
}
