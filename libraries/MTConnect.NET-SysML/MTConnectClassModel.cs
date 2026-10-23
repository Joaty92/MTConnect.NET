using MTConnect.SysML.Xmi;
using MTConnect.SysML.Xmi.Navigation;
using MTConnect.SysML.Xmi.UML;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MTConnect.SysML
{
    /// <summary>
    /// A parsed MTConnect class: its identity, inheritance (primary base plus
    /// any additional generalizations rendered as marker interfaces),
    /// properties, description, and valid version range. The cross-package
    /// parent resolver (<see cref="ResolveDanglingParents"/>) operates on
    /// instances of this type.
    /// </summary>
    public class MTConnectClassModel : IMTConnectExportModel
    {
        /// <inheritdoc/>
        public string UmlId { get; set; }

        /// <inheritdoc/>
        public string Id { get; set; }

        /// <summary>
        /// True when the class is emitted as <c>abstract</c>.
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// The class name as emitted in the generated C#.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The name of the primary base class (the single C# class base), or
        /// <c>null</c> when the class is root-level.
        /// </summary>
        public string ParentName { get; set; }

        /// <summary>
        /// xmi:id of the generalization target. Captured at parse time so the
        /// dangling-parent resolver (see <see cref="ResolveDanglingParents"/>)
        /// can look up the parent UmlClass globally even when its name is
        /// ambiguous (multiple UML classes can share a name across packages).
        /// </summary>
        public string ParentUmlId { get; set; }

        /// <summary>
        /// Names of additional generalizations beyond the primary
        /// <see cref="ParentName"/>. C# allows a single class base, so a
        /// SysML class with multiple generalizations contributes one
        /// generalization to <see cref="ParentName"/> (the class base —
        /// abstract preferred per the heuristic in the constructor) and
        /// emits the rest as marker interfaces on both the class and
        /// interface declarations.
        /// </summary>
        public List<string> AdditionalParentNames { get; set; } = new();

        /// <summary>
        /// xmi:id list paired one-to-one with <see cref="AdditionalParentNames"/>,
        /// used by the dangling-parent resolver to graft any of the
        /// additional generalizations that live in another SysML package.
        /// </summary>
        public List<string> AdditionalParentUmlIds { get; set; } = new();

        /// <summary>
        /// The cleaned description text emitted into the doc comment.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The class's properties, ordered by name.
        /// </summary>
        public List<MTConnectPropertyModel> Properties { get; set; } = new();

        /// <summary>
        /// The MTConnect version this class was deprecated at, or
        /// <c>null</c> when it is not deprecated.
        /// </summary>
        public Version MaximumVersion { get; set; }

        /// <summary>
        /// The MTConnect version this class was introduced in, or
        /// <c>null</c> when no introduction is recorded.
        /// </summary>
        public Version MinimumVersion { get; set; }

        /// <summary>
        /// Raw <c>deprecated</c> version literal captured from the
        /// <see cref="MTConnect.SysML.Xmi.Profile.Normative"/> stereotype
        /// on the source UML class (vendored from mtconnect/
        /// MtconnectTranspiler v2.8), or <c>null</c> when the class is
        /// not deprecated on its Normative stereotype. Preserved as a
        /// string so the exact upstream literal (e.g. <c>"1.4"</c> vs
        /// <c>"1.4.0"</c>) round-trips into the emitted
        /// <c>[Obsolete("Deprecated in v{Version}")]</c> attribute.
        /// </summary>
        public string Deprecated { get; set; }

        /// <summary>
        /// OCL constraint bodies attached to the source UML class,
        /// captured via <see cref="MTConnect.SysML.Xmi.UML.UmlConstraint.Body"/>.
        /// One entry per <c>&lt;ownedRule xmi:type='uml:Constraint'&gt;</c>
        /// whose specification supplies a non-empty body. Empty when the
        /// class carries no constraints or when every constraint has an
        /// empty body. Enables downstream <c>Rules[]</c> emission on the
        /// generated class so the raw OCL expressions can be inspected
        /// at runtime rather than discarded during code-generation.
        /// </summary>
        public string[] Rules { get; set; } = Array.Empty<string>();


        /// <summary>
        /// Creates an empty model for manual population.
        /// </summary>
        public MTConnectClassModel() { }

        /// <summary>
        /// Parses a class from <paramref name="umlClass"/> under model id
        /// <paramref name="id"/>: resolves its generalizations, cleans its
        /// description, and parses its properties (excluding the derived
        /// <c>made*</c>/<c>is*</c>/<c>observes*</c> navigations), ordered by
        /// name.
        /// </summary>
        public MTConnectClassModel(XmiDocument xmiDocument, string id, UmlClass umlClass)
        {
            if (umlClass != null)
            {
                UmlId = umlClass.Id;

                Id = id;
                Name = umlClass.Name;
                IsAbstract = umlClass.IsAbstract;

                // SuperClass selection across multiple generalizations.
                //
                // A SysML class may declare more than one <generalization>
                // (multi-inheritance in UML — e.g. v2.7's OriginDataSet
                // generalizes both DataSet and AbstractOrigin). C# allows a
                // single class base, so one generalization becomes
                // ParentName / ParentUmlId and the rest land in the
                // Additional* lists, where the templates render them as
                // marker interfaces.
                //
                // Heuristic for picking the primary base:
                //  1. Prefer an abstract generalization — C# convention is
                //     "abstract classes as primary inheritance, concrete /
                //     marker types as secondary interfaces". The abstract
                //     parent is also the polymorphism contract (e.g.
                //     AbstractOrigin) the consumer code reasons about.
                //  2. On a tie (multiple abstract or multiple concrete),
                //     fall back to xmi:id ascending order — a deterministic
                //     stable ordering so regen is reproducible.
                SelectGeneralizations(xmiDocument, umlClass);

                // Chain `?.` through the FirstOrDefault() result — when Comments is
                // non-null but empty, FirstOrDefault returns null and `.Body` would NRE.
                var description = umlClass.Comments?.FirstOrDefault()?.Body;
                Description = ModelHelper.ProcessDescription(description);

                // Normative.Deprecated (v2.8 widening) — read straight through
                // as the raw string version. See property XML doc for why the
                // legacy Version.TryParse path is deliberately skipped.
                Deprecated = MTConnectVersion.LookupNormativeDeprecated(xmiDocument, umlClass.Id);

                // Rules[] from UML constraint bodies. Filter out entries with
                // null / empty bodies so a stereotyped-only constraint (no OCL
                // specification child) does not surface as an empty rule.
                // Preserve source-declaration order — the OCL specification is
                // spec-authored and the ordering is meaningful for consumers
                // that report violations back to the operator.
                var constraintBodies = umlClass.Constraints?
                    .Where(c => !string.IsNullOrEmpty(c?.Body))
                    .Select(c => c.Body)
                    .ToArray();
                Rules = constraintBodies ?? Array.Empty<string>();

                // Load Properties — guard `o.Name != null` per element. The
                // outer `?.` only protects the collection; an element with null Name
                // would NRE on `o.Name.StartsWith(...)`.
                var umlProperties = umlClass.Properties?.Where(o => o.Name != null
                    && !o.Name.StartsWith("made")
                    && !o.Name.StartsWith("is")
                    && !o.Name.StartsWith("observes"));
                if (umlProperties != null)
                {
                    var propertyModels = new List<MTConnectPropertyModel>();

                    foreach (var umlProperty in umlProperties)
                    {
                        propertyModels.Add(new MTConnectPropertyModel(xmiDocument, id, umlProperty));
                    }

                    Properties = propertyModels.OrderBy(o => o.Name).ToList();
                }
            }
        }

        /// <summary>
        /// Resolves <see cref="ParentName"/> / <see cref="ParentUmlId"/>
        /// (the primary class base) and the parallel
        /// <see cref="AdditionalParentNames"/> /
        /// <see cref="AdditionalParentUmlIds"/> lists (rendered as marker
        /// interfaces) from <c>umlClass.Generalizations</c>. See the call
        /// site comment for the abstract-first / xmi:id-stable heuristic.
        /// </summary>
        private void SelectGeneralizations(XmiDocument xmiDocument, UmlClass umlClass)
        {
            var generalizations = umlClass.Generalizations;
            if (generalizations == null || generalizations.Length == 0) return;

            // Resolve each generalization's target once. Skip entries whose
            // `general` attribute is missing — defensive against malformed
            // XMI; a generalization without a target is unusable here.
            // Also filter out same-name targets (a sibling class living in
            // a different package that happens to share the parent's name —
            // emitting it as an additional interface would produce a
            // self-referential `IFoo : IFoo`).
            var resolved = generalizations
                .Where(g => !string.IsNullOrEmpty(g.General))
                .Select(g => new
                {
                    Generalization = g,
                    TargetClass = ModelHelper.GetClass(xmiDocument, g.General),
                    TargetName = ModelHelper.GetClassName(xmiDocument, g.General),
                })
                .Where(r => !string.IsNullOrEmpty(r.TargetName) && r.TargetName != Name)
                .ToList();

            if (resolved.Count == 0) return;

            // Stable ordering: abstract first (primary class base), then by
            // generalization xmi:id ascending — deterministic so regen is
            // reproducible regardless of XMI declaration order.
            var ordered = resolved
                .OrderByDescending(r => r.TargetClass != null && r.TargetClass.IsAbstract)
                .ThenBy(r => r.Generalization.Id, StringComparer.Ordinal)
                .ToList();

            var primary = ordered[0];
            ParentUmlId = primary.Generalization.General;
            ParentName = primary.TargetName;

            for (var i = 1; i < ordered.Count; i++)
            {
                AdditionalParentUmlIds.Add(ordered[i].Generalization.General);
                AdditionalParentNames.Add(ordered[i].TargetName);
            }
        }

        /// <summary>
        /// Adds the given properties to this class, skipping any whose name
        /// already exists so an inherited property is not duplicated.
        /// </summary>
        public void AddProperties(IEnumerable<MTConnectPropertyModel> properties)
        {
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    if (!Properties.Any(o => o.Name == property.Name))
                    {
                        Properties.Add(property);
                    }
                }
            }
        }

        /// <summary>
        /// Parses every class in <paramref name="umlClasses"/> under
        /// <paramref name="idPrefix"/>, skipping single-valued wrapper
        /// classes (see <see cref="ModelHelper.IsValueClass"/>) which are
        /// collapsed into scalar value types elsewhere.
        /// </summary>
        public static IEnumerable<MTConnectClassModel> Parse(XmiDocument xmiDocument, string idPrefix, IEnumerable<UmlClass> umlClasses)
        {
            var models = new List<MTConnectClassModel>();

            if (umlClasses != null)
            {
                foreach (var umlClass in umlClasses)
                {
                    var id = $"{idPrefix}.{umlClass.Name.ToTitleCase()}";

                    if (!ModelHelper.IsValueClass(umlClass))
                    {
                        models.Add(new MTConnectClassModel(xmiDocument, id, umlClass));
                    }
                }
            }

            return models;
        }

        /// <summary>
        /// Resolves dangling generalization references — classes whose
        /// <see cref="ParentName"/> targets a UML class that isn't part of the
        /// supplied <paramref name="classes"/> set. For each missing parent,
        /// the resolver looks up the class in the global XMI by its xmi:id
        /// (the authoritative reference, not the name — multiple UML classes
        /// can share a name across packages) and grafts a freshly-parsed
        /// model instance into <paramref name="classes"/> under the supplied
        /// <paramref name="idPrefix"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each per-package parser in <c>MTConnect.SysML.Models.*</c> walks a
        /// fixed sub-tree of the XMI. When a class in one sub-tree extends a
        /// class living in another (e.g. <c>Devices.Configurations.AxisDataSet</c>
        /// ⇒ <c>Observation.Representations.DataSet</c> in v2.7+), the parent
        /// is invisible to the child's parser. Without this fix-up the
        /// generator emits the child referencing a non-existent C# type and
        /// the build fails with <c>CS0246: type 'DataSet' could not be found</c>.
        /// </para>
        /// <para>
        /// The resolver is version-agnostic — it fires only when there's a
        /// dangling parent name, so older XMIs (no cross-package parents) are
        /// no-ops. The grafted parent has its own
        /// <see cref="ParentName"/> + <see cref="ParentUmlId"/> stripped
        /// (see pruning block in the implementation), so the dangling chain
        /// terminates at the graft and a single pass converges.
        /// </para>
        /// </remarks>
        public static void ResolveDanglingParents(XmiDocument xmiDocument, List<MTConnectClassModel> classes, string idPrefix)
        {
            if (xmiDocument == null || classes == null || classes.Count == 0) return;

            // Single-pass: each grafted parent has its ParentName / ParentUmlId
            // stripped (see pruning block below), so the dangling chain
            // terminates the moment the parent is grafted. The previous
            // `while (true)` wrapper around the same body added no behavior
            // and silently swallowed pathological cycles if a cap had been
            // present.

            // Ambient-aware known-parent set. When an IdCacheContext is
            // active on this thread — the intended installation site is
            // MTConnectModel.Parse, so every per-package parser sees a
            // shared cache — seed the known-UmlId set from the ambient
            // dictionary so a dangling parent already parsed by a sibling
            // package parser is recognised without a redundant graft. When
            // no context is active the ambient dictionary is absent and
            // behaviour reduces to the previous per-call scratch HashSet
            // (parity with the pre-vendor behaviour). This is the consumer
            // swap that lets the O(1) hash-lookup replace the per-call
            // O(n) scan; the vendored IdCacheContext / IdCacheContextHolder
            // pair supplies the thread-scoped storage.
            var ambient = IdCacheContextHolder.Current;
            var localUmlIds = new HashSet<string>();
            if (ambient != null)
            {
                foreach (var key in ambient.IdCache.Keys) localUmlIds.Add(key);
            }
            foreach (var c in classes)
            {
                if (string.IsNullOrEmpty(c.UmlId)) continue;
                localUmlIds.Add(c.UmlId);
                ambient?.AddToCache(c.UmlId, c);
            }

            // Dedupe missing parents via HashSet.Add rather than GroupBy/First —
            // same first-wins semantics with one allocation instead of an
            // intermediate grouping. Walks both the primary
            // (ParentName / ParentUmlId) and additional generalizations
            // (AdditionalParentNames / AdditionalParentUmlIds) so a
            // multi-inheritance class with one local + one cross-package
            // generalization still grafts the foreign parent.
            var seenParents = new HashSet<string>();
            var missing = new List<string>();
            foreach (var c in classes)
            {
                if (!string.IsNullOrEmpty(c.ParentName)
                    && !string.IsNullOrEmpty(c.ParentUmlId)
                    && !localUmlIds.Contains(c.ParentUmlId)
                    && seenParents.Add(c.ParentUmlId))
                {
                    missing.Add(c.ParentUmlId);
                }

                for (var i = 0; i < c.AdditionalParentUmlIds.Count; i++)
                {
                    var umlId = c.AdditionalParentUmlIds[i];
                    if (string.IsNullOrEmpty(umlId)) continue;
                    if (localUmlIds.Contains(umlId)) continue;
                    if (seenParents.Add(umlId)) missing.Add(umlId);
                }
            }

            if (missing.Count == 0) return;

            foreach (var danglingUmlId in missing)
            {
                var parentClass = ModelHelper.GetClass(xmiDocument, danglingUmlId);
                if (parentClass == null) continue;

                // Avoid double-grafting: a different dangling sibling may
                // already have pulled the same UmlClass into the local set.
                if (!localUmlIds.Add(parentClass.Id)) continue;

                var graftedId = $"{idPrefix}.{parentClass.Name.ToTitleCase()}";
                var grafted = new MTConnectClassModel(xmiDocument, graftedId, parentClass);

                // Pruning: a class living in another SysML package frequently brings along its own
                // generalization chain (e.g. DataSet ⇒ Representation ⇒ Observation) and properties whose
                // declared types live in yet more foreign packages (e.g. DataSet.Result : Entry). Grafting
                // the full transitive closure across namespace boundaries is rarely what we want — the
                // child sub-classes that triggered the graft (e.g. AxisDataSet, OriginDataSet) declare
                // their own concrete fields and only need a structurally-minimal C# base to extend.
                //
                // So we strip:
                //  - ParentName + ParentUmlId — the grafted class becomes a top-level base in the local
                //    namespace, terminating the recursive search rather than chasing it across packages.
                //  - Properties — their datatypes may reference yet more classes outside the local set,
                //    causing CS0246 cascades. The original child sub-classes already define every concrete
                //    field they need; the grafted base contributes structure (`: DataSet`, `: IDataSet`)
                //    rather than fields.
                //
                // If a future XMI introduces a cross-package base that genuinely needs to carry fields
                // (and those fields' datatypes are resolvable in the local namespace), revisit this
                // pruning — for now it is the safe minimum.
                grafted.ParentName = null;
                grafted.ParentUmlId = null;
                grafted.AdditionalParentNames = new List<string>();
                grafted.AdditionalParentUmlIds = new List<string>();
                grafted.Properties = new List<MTConnectPropertyModel>();

                classes.Add(grafted);

                // Register the graft in the ambient cache so a subsequent
                // ResolveDanglingParents call on a sibling package's class
                // list sees the newly-grafted parent as already-known and
                // does not re-graft it. No-op when no context is active.
                if (!string.IsNullOrEmpty(grafted.UmlId))
                {
                    ambient?.AddToCache(grafted.UmlId, grafted);
                }
            }
        }
    }
}
