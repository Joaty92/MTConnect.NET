// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed onto MTConnect.SysML.Xmi.MagicDrawProfile;
// XmlHelper references retargeted to the fork's XmiHelper.
using System;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:additionalElementImport /&gt;</c>
    /// stereotype application. Marks a UML ElementImport that MagicDraw
    /// synthesised in addition to the model author's explicit imports.
    /// </summary>
    [Serializable, XmlRoot(ElementName = XmiHelper.MagicDrawProfileStructure.ADDITIONAL_ELEMENT_IMPORT, Namespace = XmiHelper.MagicDraw_ProfileNamespace)]
    public class AdditionalElementImport : ProfileElement
    {
        /// <summary>
        /// <c>base_ElementImport</c> attribute. Foreign key to the
        /// <see cref="XmiElement.Id"/> of the ElementImport this
        /// stereotype application applies to.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.baseElementImport, Namespace = "")]
        public string? BaseElementImport { get; set; }

        /// <summary>
        /// <c>treatAsAuxiliaryInOwningProject</c> attribute. When
        /// <c>"true"</c>, MagicDraw treats the import as auxiliary within
        /// its owning project (secondary content that isn't part of the
        /// primary export).
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.MagicDrawProfileStructure.treatAsAuxiliaryInOwningProject, Namespace = "")]
        public string? TreatAsAuxiliaryInOwningProject { get; set; }
    }
}
