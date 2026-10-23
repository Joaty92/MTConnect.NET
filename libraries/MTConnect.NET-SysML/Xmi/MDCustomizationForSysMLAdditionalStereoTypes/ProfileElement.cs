// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed from
// MtconnectTranspiler.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
// onto MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes.
// Adapted: the upstream Id setter is a plain auto-property; the fork
// pushes into IdCacheContextHolder to match the sibling
// ConceptModelingProfile / MagicDrawProfile ProfileElement classes and
// so MDCustomization stereotypes participate in cross-package id
// resolution alongside them.
using MTConnect.SysML.Xmi.Navigation;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MDCustomizationForSysMLAdditionalStereoTypes
{
    /// <summary>
    /// <c>&lt;MD_Customization_for_SysML__additional_stereotypes:x /&gt;</c>
    /// element base class. Every additional stereotype application
    /// inherits its <c>xmi:id</c> attribute from here.
    /// </summary>
    public abstract class ProfileElement
    {
        private string? _id;

        /// <summary>
        /// <c>xmi:id</c> attribute. Setting a non-empty id publishes the
        /// element into the ambient <see cref="IdCacheContextHolder"/>.
        /// </summary>
        [XmlAttribute(AttributeName = XmiHelper.XmiStructure.id, Namespace = XmiHelper.XmiNamespace)]
        public virtual string? Id
        {
            get { return _id; }
            set
            {
                _id = value;
                if (!string.IsNullOrEmpty(_id))
                    IdCacheContextHolder.Current?.AddToCache(_id!, this);
            }
        }
    }
}
