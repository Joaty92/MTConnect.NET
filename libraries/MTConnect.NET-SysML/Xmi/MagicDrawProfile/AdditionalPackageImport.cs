// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:additionalPackageImport /&gt;</c>
    /// stereotype application. Marks a UML PackageImport that MagicDraw
    /// synthesised on top of the model author's explicit imports.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.ADDITIONAL_PACKAGE_IMPORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class AdditionalPackageImport : ProfileElement
    {
        /// <summary>
        /// <c>base_PackageImport</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the PackageImport this
        /// stereotype application applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.basePackageImport, Namespace = "")]
        public string? BasePackageImport { get; set; }

        /// <summary>
        /// <c>treatAsAuxiliaryInOwningProject</c> attribute. When
        /// <c>"true"</c>, MagicDraw treats the import as auxiliary within
        /// its owning project.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.treatAsAuxiliaryInOwningProject, Namespace = "")]
        public string? TreatAsAuxiliaryInOwningProject { get; set; }
    }
}
