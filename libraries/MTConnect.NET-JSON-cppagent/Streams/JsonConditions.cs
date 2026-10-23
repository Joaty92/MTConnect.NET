// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using MTConnect.Observations;
using MTConnect.Observations.Output;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MTConnect.Streams.Json
{
    /// <summary>
    /// Sequence of Condition observations in the cppagent JSON v2 shape:
    /// a flat JSON array of single-key <see cref="JsonConditionWrapper"/>
    /// objects, one wrapper per observation, in insertion order.
    /// </summary>
    /// <remarks>
    /// The type shape IS the wire shape — <see cref="JsonConditions"/>
    /// inherits <see cref="List{T}"/> of <see cref="JsonConditionWrapper"/>
    /// so System.Text.Json's default serializer handles both directions
    /// with no custom converter. Emitting bytes:
    /// <code>[{"Normal":{...}},{"Warning":{...}},{"Fault":{...}}]</code>
    /// requires only that each wrapper carries exactly one non-null
    /// property (enforced by convention, not by runtime validation) and
    /// that the four properties on <see cref="JsonConditionWrapper"/> are
    /// annotated with
    /// <c>[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]</c>.
    /// <para>
    /// Element ordering on the wire follows list insertion order. The
    /// <see cref="JsonConditions(IEnumerable{IObservationOutput})"/> ctor
    /// preserves the historical level-order emission
    /// (<c>FAULT</c> → <c>WARNING</c> → <c>NORMAL</c> → <c>UNAVAILABLE</c>,
    /// source order within each level) so this rewrite is byte-identical
    /// on the wire to the pre-refactor converter output for the same
    /// observation input <b>when the emission pipeline supplies the same
    /// <see cref="System.Text.Json.JsonSerializerOptions"/> the converter
    /// path used</b> (in particular, per-property
    /// <c>[JsonIgnore(WhenWritingNull)]</c> makes wrapper output single-key
    /// regardless of the ambient <c>DefaultIgnoreCondition</c>). Callers
    /// constructing via the copy ctor or direct
    /// <see cref="List{T}.Add(T)"/> control ordering explicitly.
    /// </para>
    /// </remarks>
    public sealed class JsonConditions : List<JsonConditionWrapper>
    {
        /// <summary>
        /// Initializes an empty container for JSON deserialization or
        /// programmatic construction via <see cref="List{T}.Add(T)"/>.
        /// </summary>
        public JsonConditions() { }

        /// <summary>
        /// Initializes the container with a pre-built sequence of
        /// wrappers. Order is preserved from
        /// <paramref name="wrappers"/>.
        /// </summary>
        /// <param name="wrappers">
        /// The wrapper sequence to seed the list with. A
        /// <see langword="null"/> reference is treated as an empty
        /// sequence.
        /// </param>
        public JsonConditions(IEnumerable<JsonConditionWrapper> wrappers)
            : base(wrappers ?? Array.Empty<JsonConditionWrapper>())
        {
        }

        /// <summary>
        /// Initializes the container from an observation-output
        /// sequence, wrapping each observation in the single-key wrapper
        /// for its level. Preserves the historical level-order emission
        /// (<c>FAULT</c> → <c>WARNING</c> → <c>NORMAL</c> → <c>UNAVAILABLE</c>,
        /// source order within each level) so the wire output is
        /// byte-identical to the pre-refactor converter for the same
        /// input.
        /// </summary>
        /// <param name="observations">
        /// The observation-output sequence to project into wrappers. A
        /// <see langword="null"/> reference and <see langword="null"/>
        /// entries are skipped; entries whose
        /// <see cref="ValueKeys.Level"/> value does not match a
        /// <see cref="ConditionLevel"/> arm are dropped (matches the
        /// pre-refactor converter). The sequence is enumerated exactly
        /// once: the ctor buffers it into a local list before the four
        /// level-partition passes so caller-supplied lazy / deferred
        /// enumerables (LINQ queries, generators) are not re-evaluated.
        /// </param>
        public JsonConditions(IEnumerable<IObservationOutput> observations)
        {
            if (observations == null) return;

            // Buffer once — the four level partitions each need a full
            // pass, and caller-supplied lazy sequences (LINQ queries,
            // generators, side-effecting iterators) must not be
            // re-evaluated.
            var buffered = new List<IObservationOutput>();
            foreach (var observation in observations)
            {
                if (observation != null) buffered.Add(observation);
            }

            AppendLevel(buffered, LevelNameFault, JsonConditionWrapper.OfFault);
            AppendLevel(buffered, LevelNameWarning, JsonConditionWrapper.OfWarning);
            AppendLevel(buffered, LevelNameNormal, JsonConditionWrapper.OfNormal);
            AppendLevel(buffered, LevelNameUnavailable, JsonConditionWrapper.OfUnavailable);
        }

        /// <summary>
        /// Materializes every wrapper into a flat
        /// <see cref="List{T}"/> of <see cref="IObservation"/>, in list
        /// insertion order. Wrappers whose four level properties are all
        /// <see langword="null"/> are skipped. Not serialized.
        /// </summary>
        [JsonIgnore]
        public List<IObservation> Observations
        {
            get
            {
                var result = new List<IObservation>(Count);
                foreach (var wrapper in this)
                {
                    var observation = wrapper?.ToObservation();
                    if (observation != null) result.Add(observation);
                }
                return result;
            }
        }

        // Level names materialised once — enum ToString() would
        // otherwise allocate a fresh string per AppendLevel call.
        private const string LevelNameFault = nameof(ConditionLevel.FAULT);
        private const string LevelNameWarning = nameof(ConditionLevel.WARNING);
        private const string LevelNameNormal = nameof(ConditionLevel.NORMAL);
        private const string LevelNameUnavailable = nameof(ConditionLevel.UNAVAILABLE);

        private void AppendLevel(
            List<IObservationOutput> observations,
            string levelName,
            Func<JsonCondition, JsonConditionWrapper> factory)
        {
            foreach (var observation in observations)
            {
                if (observation.GetValue(ValueKeys.Level) == levelName)
                {
                    Add(factory(new JsonCondition(observation)));
                }
            }
        }
    }
}
