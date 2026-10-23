// Vendored from mtconnect/MtconnectTranspiler @ v2.8 (Apache-2.0);
// namespace re-homed from MtconnectTranspiler.Contracts.Navigation
// onto MTConnect.SysML.Xmi.Navigation to fold into the fork's Xmi/
// tree. Adapted to add a nullable annotation on the dictionary
// value type (fork uses <Nullable>annotations</Nullable> project-
// wide, upstream doesn't).
using System;
using System.Collections.Generic;

namespace MTConnect.SysML.Xmi.Navigation
{
    /// <summary>
    /// A thread-scoped cache for O(1) cross-package reference resolution
    /// during XMI deserialisation. Vendored from upstream
    /// mtconnect/MtconnectTranspiler v2.8's IdCache infrastructure so the
    /// fork's <c>ResolveDanglingParents</c> post-parse walk (O(n*m) in the
    /// worst case) can be replaced with a direct hash-lookup path in
    /// subsequent commits.
    /// </summary>
    /// <remarks>
    /// The context is thread-scoped via <see cref="IdCacheContextHolder"/>
    /// so parallel XMI deserialisation (one per test fixture, one per CI
    /// job) doesn't cross-pollute caches. Construct one per parse pass,
    /// dispose to clear the thread-static holder.
    /// </remarks>
    public class IdCacheContext : IDisposable
    {
        /// <summary>
        /// Gets the dictionary used for caching object instances by their
        /// XMI id. Populated as elements are deserialised; consulted when
        /// cross-package references are resolved.
        /// </summary>
        public Dictionary<string, object> IdCache { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Initialises a new instance of <see cref="IdCacheContext"/> and
        /// installs it as the current thread's cache. Throws when a
        /// context is already active on the current thread — nested
        /// contexts would collide silently on the shared dictionary.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A context is already active on this thread.
        /// </exception>
        public IdCacheContext()
        {
            if (IdCacheContextHolder.Current != null)
            {
                throw new InvalidOperationException("An IdCacheContext is already active on this thread.");
            }

            IdCacheContextHolder.Current = this;
        }

        /// <summary>
        /// Adds an object to the cache with the specified id. No-op when
        /// the id is <c>null</c> or empty (unnamed XMI elements do not
        /// belong in a lookup cache).
        /// </summary>
        /// <param name="id">The XMI id of the element.</param>
        /// <param name="element">The element to cache.</param>
        public void AddToCache(string id, object element)
        {
            if (!string.IsNullOrEmpty(id))
            {
                IdCache[id] = element;
            }
        }

        /// <summary>
        /// Retrieves an object from the cache by its XMI id.
        /// </summary>
        /// <param name="id">The XMI id of the element to retrieve.</param>
        /// <returns>
        /// The cached element, or <c>null</c> when the id is not present
        /// in the cache. Callers that need to distinguish "not cached"
        /// from "cached-null" should not rely on this convenience method.
        /// </returns>
        public object? GetFromCache(string id)
        {
            if (IdCache.TryGetValue(id, out var element))
            {
                return element;
            }
            return null;
        }

        /// <summary>
        /// Disposes of the context, clearing the thread-static holder so
        /// the next <see cref="IdCacheContext"/> constructed on this
        /// thread starts fresh. The underlying dictionary is not
        /// eagerly cleared — GC reclaims it when the caller drops the
        /// reference.
        /// </summary>
        public void Dispose()
        {
            IdCacheContextHolder.Current = null;
        }
    }
}
