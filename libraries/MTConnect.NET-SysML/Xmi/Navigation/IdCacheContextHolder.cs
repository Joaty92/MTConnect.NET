// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed from MtconnectTranspiler.Contracts.Navigation
// onto MTConnect.SysML.Xmi.Navigation to fold into the fork's Xmi/
// tree.
namespace MTConnect.SysML.Xmi.Navigation
{
    /// <summary>
    /// Holds the current thread's active <see cref="IdCacheContext"/>. A
    /// thread-static ambient so deserialisation code paths can consult
    /// the cache without threading the reference through every call.
    /// </summary>
    /// <remarks>
    /// Thread-static so parallel test fixtures / CI job shards cannot
    /// cross-pollute caches. A single-threaded caller sees one cache
    /// per <c>using</c> block over <see cref="IdCacheContext"/>.
    /// </remarks>
    public static class IdCacheContextHolder
    {
        [System.ThreadStatic]
        private static IdCacheContext? _current;

        /// <summary>
        /// Gets or sets the current <see cref="IdCacheContext"/> for the
        /// current thread. Set to <c>null</c> when no context is active
        /// (e.g. between deserialisation passes).
        /// </summary>
        public static IdCacheContext? Current
        {
            get { return _current; }
            set { _current = value; }
        }
    }
}
