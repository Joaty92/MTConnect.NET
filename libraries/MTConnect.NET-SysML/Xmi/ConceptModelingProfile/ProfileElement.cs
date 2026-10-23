// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed from MtconnectTranspiler.Xmi.ConceptModelingProfile
// onto MTConnect.SysML.Xmi.ConceptModelingProfile to fold into the
// fork's Xmi/ tree. The upstream ProfileElement.Id setter pushes into
// MtconnectTranspiler.Contracts.Navigation.IdCacheContextHolder; the
// fork keeps the same behaviour by pushing into the equivalent
// MTConnect.SysML.Xmi.Navigation.IdCacheContextHolder, so stereotype
// applications participate in cross-package reference resolution the
// same way UML packaged elements do.
using MTConnect.SysML.Xmi.Navigation;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.ConceptModelingProfile
{
    /// <summary>
    /// <c>&lt;Concept_Modeling_Profile:x /&gt;</c> element base class.
    /// Every Concept_Modeling_Profile stereotype application inherits
    /// its <c>xmi:id</c> attribute (and the IdCache side-effect on set)
    /// from here.
    /// </summary>
    public abstract class ProfileElement
    {
        private string? _id;

        /// <summary>
        /// <c>xmi:id</c> attribute. Setting a non-empty id publishes the
        /// element into the ambient <see cref="IdCacheContextHolder"/>
        /// so cross-package reference resolution (e.g. from
        /// <c>base_Class</c> / <c>base_Property</c> foreign keys) can
        /// look it up in O(1).
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
