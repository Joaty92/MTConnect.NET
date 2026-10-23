using MTConnect.SysML.Xmi.ConceptModelingProfile;
using MTConnect.SysML.Xmi.MagicDrawProfile;
using MTConnect.SysML.Xmi.Profile;
using MTConnect.SysML.Xmi.UML;
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi
{
    /// <summary>
    /// <c>xmi:XMI</c> element
    /// </summary>
    [Serializable, XmlRoot(ElementName = "XMI", Namespace = XmiHelper.XmiNamespace)]
    public class XmiDocument
    {
        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.XmiDocumentation"/>
        /// </summary>
        public XmiDocumentation? Documentation { get; set; }

        /// <summary>
        /// Child <inheritdoc cref="MTConnect.SysML.Xmi.UML.UmlModel"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.XmiStructure.MODEL, Namespace = XmiHelper.UmlNamespace)]
        public UmlModel? Model { get; set; }

        #region Profile
        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.Profile.Normative"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ProfileStructure.NORMATIVE, Namespace = XmiHelper.ProfileNamespace)]
        public Normative[]? NormativeIntroductions { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.Profile.Deprecated"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ProfileStructure.DEPRECATED, Namespace = XmiHelper.ProfileNamespace)]
        public Deprecated[]? Deprecations { get; set; }
        #endregion

        #region Concept_Modeling_Profile
        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.Anything"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.ANYTHING, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public Anything[]? Anythings { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.DisjointWith"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.DISJOINT_WITH, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public DisjointWith[]? DisjointsWith { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.EquivalentClass"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.EQUIVALENT_CLASS, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public EquivalentClass[]? EquivalentClasses { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.Functional"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.FUNCTIONAL, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public Functional[]? Functionals { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.LiteralAnnotation"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.LITERAL_ANNOTATION, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public LiteralAnnotation[]? LiteralAnnotations { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.Resource"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.RESOURCE, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public Resource[]? Resources { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.Restriction"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.RESTRICTION, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public Restriction[]? Restrictions { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.ConceptModelingProfile.Transitive"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.ConceptModelingProfileStructure.TRANSITIVE, Namespace = XmiHelper.Concept_Modeling_ProfileNamespace)]
        public Transitive[]? Transitives { get; set; }
        #endregion

        #region MagicDraw_Profile
        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.AdditionalElementImport"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.ADDITIONAL_ELEMENT_IMPORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public AdditionalElementImport[]? AdditionalElementImports { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.AdditionalPackageImport"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.ADDITIONAL_PACKAGE_IMPORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public AdditionalPackageImport[]? AdditionalPackageImports { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.CustomSort"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.CUSTOM_SORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public CustomSort[]? CustomSorts { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.DiagramInfo"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.DIAGRAM_INFO, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public DiagramInfo[]? DiagramInfos { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.DiagramTable"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.DIAGRAM_TABLE, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public DiagramTable[]? DiagramTables { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MagicDrawProfile.InstanceTable"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MagicDrawProfileStructure.INSTANCE_TABLE, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
        public InstanceTable[]? InstanceTables { get; set; }
        #endregion

        #region MD_Customization_for_SysML__additional_stereotypes
        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.ValueProperty"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.VALUE_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.ValueProperty[]? ValueProperties { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.PartProperty"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.PART_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.PartProperty[]? PartProperties { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.ReferenceProperty"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.REFERENCE_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.ReferenceProperty[]? ReferenceProperties { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.ConstraintProperty"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.CONSTRAINT_PROPERTY, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.ConstraintProperty[]? ConstraintProperties { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.ConstraintParameter"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.CONSTRAINT_PARAMETER, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.ConstraintParameter[]? ConstraintParameters { get; set; }

        /// <summary>
        /// Collection of <inheritdoc cref="MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.ExternalModel"/>
        /// </summary>
        [XmlElement(ElementName = XmiHelper.MDCustomizationForSysMLAdditionalStereoTypes.EXTERNAL_MODEL, Namespace = XmiHelper.Md_Customization_for_SysML__additional_stereotypesNamespace)]
        public MDCustomizationForSysMLAdditionalStereoTypes.ExternalModel[]? ExternalModels { get; set; }
        #endregion
    }
}