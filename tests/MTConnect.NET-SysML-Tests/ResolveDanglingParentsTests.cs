using System.Collections.Generic;
using MTConnect.SysML;
using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.Navigation;
using MTConnect.SysML.Xmi.UML;
using NUnit.Framework;

namespace MTConnect.Tests.SysML
{
    /// <summary>
    /// Coverage on <see cref="MTConnectClassModel.ResolveDanglingParents"/>
    /// after the IdCache consumer swap. Two axes are exercised:
    /// <list type="bullet">
    ///   <item>parity — with no ambient <see cref="IdCacheContext"/> active,
    ///     the resolver behaves exactly as the pre-swap implementation:
    ///     null / empty inputs are no-ops, in-list parents are not
    ///     re-grafted, and unresolvable dangling parents do not mutate
    ///     the class list; and</item>
    ///   <item>ambient-shared visibility — when an
    ///     <see cref="IdCacheContext"/> is active, the ambient cache seeds
    ///     the known-UmlId set (so a parent parsed by a sibling package's
    ///     call is recognised without a re-graft) and is populated by
    ///     every class in the current list plus every graft, so a
    ///     subsequent call on a different list carries the visibility
    ///     forward.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class ResolveDanglingParentsTests
    {
        [TearDown]
        public void ResetHolder()
        {
            // Guard against a failed test leaving the thread-static
            // holder dirty for the next test in the fixture.
            IdCacheContextHolder.Current = null;
        }

        // Non-collidable identifier prefix so any leftover ModelHelper
        // static cache from prior fixture runs cannot resolve these ids
        // to a real UmlClass. Every UmlId used in this fixture starts
        // here so `ModelHelper.GetClass(...)` returns null and the graft
        // branch cannot fire from a stale cache.
        private const string TestIdPrefix = "TEST_RESOLVE_DANGLING_";

        private static MTConnectClassModel MakeChild(
            string umlId,
            string name,
            string parentUmlId,
            string parentName)
        {
            return new MTConnectClassModel
            {
                UmlId = umlId,
                Id = "Test." + name,
                Name = name,
                ParentUmlId = parentUmlId,
                ParentName = parentName,
                AdditionalParentUmlIds = new List<string>(),
                AdditionalParentNames = new List<string>(),
                Properties = new List<MTConnectPropertyModel>()
            };
        }

        private static MTConnectClassModel MakeStandalone(string umlId, string name)
        {
            return new MTConnectClassModel
            {
                UmlId = umlId,
                Id = "Test." + name,
                Name = name,
                AdditionalParentUmlIds = new List<string>(),
                AdditionalParentNames = new List<string>(),
                Properties = new List<MTConnectPropertyModel>()
            };
        }

        [Test]
        public void Null_xmiDocument_is_a_noop()
        {
            var classes = new List<MTConnectClassModel>
            {
                MakeChild(TestIdPrefix + "child", "Child", TestIdPrefix + "parent", "Parent")
            };

            MTConnectClassModel.ResolveDanglingParents(null, classes, "Test");

            Assert.That(classes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Null_classes_list_is_a_noop()
        {
            var doc = new XmiDocument();
            Assert.DoesNotThrow(() =>
                MTConnectClassModel.ResolveDanglingParents(doc, null, "Test"));
        }

        [Test]
        public void Empty_classes_list_is_a_noop()
        {
            var doc = new XmiDocument();
            var classes = new List<MTConnectClassModel>();

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            Assert.That(classes, Is.Empty);
        }

        [Test]
        public void Parity_no_ambient_context_no_dangling_parents_leaves_list_unchanged()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var parent = MakeStandalone(TestIdPrefix + "parent-p", "Parent");
            var child = MakeChild(TestIdPrefix + "child-p", "Child", parent.UmlId, "Parent");

            var classes = new List<MTConnectClassModel> { parent, child };

            Assert.That(IdCacheContextHolder.Current, Is.Null);
            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            Assert.That(classes, Has.Count.EqualTo(2));
            Assert.That(classes, Does.Contain(parent));
            Assert.That(classes, Does.Contain(child));
        }

        [Test]
        public void Parity_no_ambient_context_unresolvable_parent_does_not_mutate_list()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var child = MakeChild(TestIdPrefix + "child-u", "Child", TestIdPrefix + "missing-parent-u", "MissingParent");

            var classes = new List<MTConnectClassModel> { child };

            Assert.That(IdCacheContextHolder.Current, Is.Null);
            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            // Parent cannot be resolved through ModelHelper (the id is
            // deliberately non-collidable), so no graft lands. Parity
            // with the pre-swap behaviour: unresolved dangling parents
            // are silently ignored.
            Assert.That(classes, Has.Count.EqualTo(1));
            Assert.That(classes[0], Is.SameAs(child));
        }

        [Test]
        public void Parity_no_ambient_context_additional_parents_walked_alongside_primary()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var child = MakeChild(TestIdPrefix + "child-a", "Child", TestIdPrefix + "primary-a", "PrimaryParent");
            child.AdditionalParentUmlIds.Add(TestIdPrefix + "extra-a");
            child.AdditionalParentNames.Add("ExtraParent");
            // A blank additional parent must be tolerated — reproduces the
            // guard on line 321 of the pre-swap implementation.
            child.AdditionalParentUmlIds.Add(string.Empty);
            child.AdditionalParentNames.Add(string.Empty);

            var classes = new List<MTConnectClassModel> { child };

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            // Neither the primary nor the additional resolves (both live
            // outside the empty test XMI), so the list is unchanged.
            Assert.That(classes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Parity_no_ambient_context_child_with_null_UmlId_is_skipped_when_seeding()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var anonymous = MakeStandalone(null, "Anonymous");
            var child = MakeChild(TestIdPrefix + "child-null", "Child", TestIdPrefix + "parent-null", "Parent");

            var classes = new List<MTConnectClassModel> { anonymous, child };

            Assert.DoesNotThrow(() =>
                MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test"));
            Assert.That(classes, Has.Count.EqualTo(2));
        }

        [Test]
        public void Parity_no_ambient_context_child_with_null_ParentName_is_skipped()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var child = new MTConnectClassModel
            {
                UmlId = TestIdPrefix + "child-nop",
                Id = "Test.Child",
                Name = "Child",
                ParentName = null,
                ParentUmlId = TestIdPrefix + "parent-nop"
            };

            var classes = new List<MTConnectClassModel> { child };

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            Assert.That(classes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Ambient_context_pre_seeded_parent_is_recognised_and_not_grafted()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var parentUmlId = TestIdPrefix + "sibling-parent";
            var child = MakeChild(TestIdPrefix + "child-s", "Child", parentUmlId, "SiblingParent");

            var classes = new List<MTConnectClassModel> { child };

            using var context = new IdCacheContext();
            // Simulate a sibling package parser having already added the
            // parent to the shared cache before this call. The parent's
            // stand-in payload is opaque — only its presence at the id
            // matters to ResolveDanglingParents.
            context.AddToCache(parentUmlId, MakeStandalone(parentUmlId, "SiblingParent"));

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            // No graft: the ambient cache already knew about the parent,
            // so the "dangling" walk short-circuits before ModelHelper is
            // consulted.
            Assert.That(classes, Has.Count.EqualTo(1));
            Assert.That(classes[0], Is.SameAs(child));

            // Side effect: the child was published to the ambient cache
            // for subsequent sibling calls.
            Assert.That(context.GetFromCache(child.UmlId), Is.SameAs(child));
        }

        [Test]
        public void Ambient_context_carries_visibility_across_sequential_calls()
        {
            var doc = new XmiDocument { Model = new UmlModel() };

            // First call: parses package α, contributes a parent class.
            var alphaParent = MakeStandalone(TestIdPrefix + "alpha-parent", "AlphaParent");
            var alphaChild = MakeChild(TestIdPrefix + "alpha-child", "AlphaChild", alphaParent.UmlId, "AlphaParent");
            var alphaClasses = new List<MTConnectClassModel> { alphaParent, alphaChild };

            // Second call: parses package β, whose child extends alphaParent
            // (a cross-package generalisation). Without the ambient cache
            // the beta call would treat alphaParent's UmlId as dangling
            // and try to graft it through ModelHelper (which cannot
            // resolve it in this test), leaving the reference unfixed.
            // With the ambient cache seeded by the alpha call, the beta
            // walk recognises alphaParent as known and skips the graft
            // attempt entirely.
            var betaChild = MakeChild(TestIdPrefix + "beta-child", "BetaChild", alphaParent.UmlId, "AlphaParent");
            var betaClasses = new List<MTConnectClassModel> { betaChild };

            using (new IdCacheContext())
            {
                MTConnectClassModel.ResolveDanglingParents(doc, alphaClasses, "Alpha");
                var alphaAfter = alphaClasses.Count;
                MTConnectClassModel.ResolveDanglingParents(doc, betaClasses, "Beta");

                Assert.That(alphaAfter, Is.EqualTo(2), "Alpha list should not have grown.");
                Assert.That(betaClasses, Has.Count.EqualTo(1),
                    "Beta list should not have grown — the ambient cache recognised the shared parent.");
                Assert.That(IdCacheContextHolder.Current, Is.Not.Null);
                Assert.That(IdCacheContextHolder.Current!.GetFromCache(alphaParent.UmlId), Is.SameAs(alphaParent));
                Assert.That(IdCacheContextHolder.Current.GetFromCache(betaChild.UmlId), Is.SameAs(betaChild));
            }

            Assert.That(IdCacheContextHolder.Current, Is.Null,
                "Context disposal must clear the ambient holder.");
        }

        [Test]
        public void Ambient_context_pre_seeded_additional_parent_is_recognised()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var extraUmlId = TestIdPrefix + "extra-parent";
            var child = MakeChild(TestIdPrefix + "child-e", "Child", TestIdPrefix + "primary-e", "PrimaryParent");
            child.AdditionalParentUmlIds.Add(extraUmlId);
            child.AdditionalParentNames.Add("ExtraParent");

            var classes = new List<MTConnectClassModel> { child };

            using var context = new IdCacheContext();
            context.AddToCache(extraUmlId, MakeStandalone(extraUmlId, "ExtraParent"));
            // Also seed the primary so both walks short-circuit and the
            // "missing" list ends up empty. This exercises the `if
            // (missing.Count == 0) return;` fast-path under an ambient
            // context.
            context.AddToCache(TestIdPrefix + "primary-e", MakeStandalone(TestIdPrefix + "primary-e", "PrimaryParent"));

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            Assert.That(classes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Ambient_context_empty_leaves_parity_behaviour_when_no_dangling_parents()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var parent = MakeStandalone(TestIdPrefix + "parent-emp", "Parent");
            var child = MakeChild(TestIdPrefix + "child-emp", "Child", parent.UmlId, "Parent");
            var classes = new List<MTConnectClassModel> { parent, child };

            using var context = new IdCacheContext();
            Assert.That(context.IdCache, Is.Empty);

            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            Assert.That(classes, Has.Count.EqualTo(2));
            // Side effect: both classes now live in the ambient cache
            // so a subsequent sibling call sees them.
            Assert.That(context.GetFromCache(parent.UmlId), Is.SameAs(parent));
            Assert.That(context.GetFromCache(child.UmlId), Is.SameAs(child));
        }

        [Test]
        public void Ambient_context_child_with_null_UmlId_is_not_registered()
        {
            var doc = new XmiDocument { Model = new UmlModel() };
            var anonymous = MakeStandalone(null, "Anonymous");
            var identified = MakeStandalone(TestIdPrefix + "identified", "Identified");
            var classes = new List<MTConnectClassModel> { anonymous, identified };

            using var context = new IdCacheContext();
            MTConnectClassModel.ResolveDanglingParents(doc, classes, "Test");

            // The anonymous class must not appear in the cache — the
            // guard on the UmlId-seed loop drops null / empty ids.
            Assert.That(context.IdCache.ContainsValue(anonymous), Is.False);
            Assert.That(context.GetFromCache(identified.UmlId), Is.SameAs(identified));
        }
    }
}
