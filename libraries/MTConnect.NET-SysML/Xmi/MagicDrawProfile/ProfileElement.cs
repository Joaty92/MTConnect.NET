// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed from MtconnectTranspiler.Xmi.MagicDrawProfile onto
// MTConnect.SysML.Xmi.MagicDrawProfile. The upstream ProfileElement.Id
// setter pushes into MtconnectTranspiler.Contracts.Navigation.IdCache-
// ContextHolder; the fork keeps the same behaviour by pushing into the
// equivalent MTConnect.SysML.Xmi.Navigation.IdCacheContextHolder so
// MagicDraw stereotype applications participate in cross-package id
// resolution alongside UML packaged elements.
using MTConnect.SysML.Xmi.Navigation;
using System.Xml.Serialization;

namespace MTConnect.SysML.Xmi.MagicDrawProfile
{
    /// <summary>
    /// <c>&lt;MagicDraw_Profile:x /&gt;</c> element base class. Every
    /// MagicDraw_Profile stereotype application inherits its <c>xmi:id</c>
    /// attribute (and the IdCache side-effect on set) from here.
    /// </summary>
    public abstract class ProfileElement
    {
        private string? _id;

        /// <summary>
        /// <c>xmi:id</c> attribute. Setting a non-empty id publishes the
        /// element into the ambient <see cref="IdCacheContextHolder"/>
        /// so cross-package reference resolution can look it up in O(1).
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
