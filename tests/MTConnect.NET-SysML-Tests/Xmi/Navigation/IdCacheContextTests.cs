using System;
using MTConnect.SysML.Xmi.Navigation;
using NUnit.Framework;

namespace MTConnect.Tests.SysML.Xmi.Navigation
{
    /// <summary>
    /// Coverage on the IdCache thread-scoped ambient vendored from
    /// mtconnect/MtconnectTranspiler v2.8. Verifies the disposal
    /// contract, the round-trip through AddToCache / GetFromCache,
    /// the null / empty id no-op guarantee, and the nested-context
    /// guard.
    /// </summary>
    [TestFixture]
    public class IdCacheContextTests
    {
        [TearDown]
        public void ResetHolder()
        {
            // Guard against a failed test leaving the thread-static
            // holder dirty for the next test in the fixture.
            IdCacheContextHolder.Current = null;
        }

        [Test]
        public void Constructor_installs_context_as_current_on_thread()
        {
            Assert.That(IdCacheContextHolder.Current, Is.Null);
            using var context = new IdCacheContext();
            Assert.That(IdCacheContextHolder.Current, Is.SameAs(context));
        }

        [Test]
        public void Dispose_clears_the_thread_holder()
        {
            var context = new IdCacheContext();
            context.Dispose();
            Assert.That(IdCacheContextHolder.Current, Is.Null);
        }

        [Test]
        public void Nested_context_throws_InvalidOperationException()
        {
            using var outer = new IdCacheContext();
            Assert.Throws<InvalidOperationException>(() => new IdCacheContext());
        }

        [Test]
        public void AddToCache_and_GetFromCache_round_trip()
        {
            using var context = new IdCacheContext();
            var element = new object();
            context.AddToCache("id-1", element);
            Assert.That(context.GetFromCache("id-1"), Is.SameAs(element));
        }

        [Test]
        public void AddToCache_overwrites_existing_entry_on_same_id()
        {
            using var context = new IdCacheContext();
            var first = new object();
            var second = new object();
            context.AddToCache("id-1", first);
            context.AddToCache("id-1", second);
            Assert.That(context.GetFromCache("id-1"), Is.SameAs(second));
        }

        [TestCase(null)]
        [TestCase("")]
        public void AddToCache_ignores_null_or_empty_id(string? id)
        {
            using var context = new IdCacheContext();
            context.AddToCache(id!, new object());
            Assert.That(context.IdCache, Is.Empty);
        }

        [Test]
        public void GetFromCache_returns_null_for_unknown_id()
        {
            using var context = new IdCacheContext();
            Assert.That(context.GetFromCache("unknown"), Is.Null);
        }
    }
}
