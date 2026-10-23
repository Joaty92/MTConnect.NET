// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using System.Linq;
using MTConnect.Observations;
using MTConnect.Shdr;
using NUnit.Framework;

namespace MTConnect.Tests.Shdr
{
    /// <summary>
    /// Pins the SHDR parse-side complement of the value-class-aware empty-Result coerce.
    /// <para>
    /// Prior to this PR <see cref="ShdrDataItem.FromString(string, bool)"/> silently DROPPED
    /// any key-value pair whose value token was missing (a trailing DataItemKey with no
    /// <c>|value</c> segment). That masked genuine empty-value updates from the adapter and
    /// prevented the agent's value-class-aware coerce from ever seeing them. The fix
    /// (<c>ShdrDataItem.FromKeyValuePairs</c>, else branch on <c>y != null</c>) preserves
    /// the DataItem with an empty-string Result so the coerce runs at the correct layer.
    /// </para>
    /// <para>
    /// These tests exercise the parser directly. They are the RED that would have failed
    /// against the pre-fix <c>ShdrDataItem</c> and turn GREEN with the fix in place. They
    /// pin the load-bearing observable: the count of parsed DataItems is one higher than
    /// the pre-fix behaviour for a trailing-key line, and the trailing DataItem carries an
    /// empty <see cref="ValueKeys.Result"/> value rather than being absent from the output.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("ShdrDataItemParseEmptyValue")]
    public class ShdrDataItemParseEmptyValueTests
    {
        private const string Timestamp = "2000-01-01T00:00:00.0000000Z";


        // -------------------------------------------------------------------- //
        // Trailing key with no |value segment (the load-bearing case)          //
        // -------------------------------------------------------------------- //

        /// <summary>
        /// A line with a single trailing key that lacks a <c>|value</c> segment yields exactly
        /// one DataItem whose Result is the empty string - previously the parser dropped it.
        /// </summary>
        [Test]
        public void FromString_TrailingKey_Without_Value_Segment_Yields_Empty_Result()
        {
            var items = ShdrDataItem.FromString($"{Timestamp}|program").ToList();

            Assert.That(items.Count, Is.EqualTo(1),
                "the trailing 'program' key with no |value MUST no longer be dropped");
            Assert.That(items[0].DataItemKey, Is.EqualTo("program"),
                "DataItemKey must survive parse");
            Assert.That(items[0].GetValue(ValueKeys.Result), Is.EqualTo(string.Empty),
                "the fix assigns an empty-string Result rather than leaving Values unset");
        }

        /// <summary>
        /// In a multi-pair line where the LAST key lacks a <c>|value</c> segment, the earlier
        /// pairs still parse normally AND the trailing key lands with an empty Result. Before
        /// the fix the parser returned only the earlier pairs; the trailing key was silently
        /// dropped and the adapter update was invisible to the agent.
        /// </summary>
        [Test]
        public void FromString_MultiPair_With_Trailing_Bare_Key_Preserves_All_Items()
        {
            var items = ShdrDataItem.FromString($"{Timestamp}|avail|AVAILABLE|program").ToList();

            Assert.That(items.Count, Is.EqualTo(2),
                "trailing bare key must not be dropped from a multi-pair line");
            Assert.That(items[0].DataItemKey, Is.EqualTo("avail"));
            Assert.That(items[0].GetValue(ValueKeys.Result), Is.EqualTo("AVAILABLE"),
                "the concrete pair MUST parse unchanged");
            Assert.That(items[1].DataItemKey, Is.EqualTo("program"));
            Assert.That(items[1].GetValue(ValueKeys.Result), Is.EqualTo(string.Empty),
                "the trailing bare key MUST land with an empty-string Result");
        }


        // -------------------------------------------------------------------- //
        // Trailing pipe (key|) - also newly recovered by the fix.              //
        // Pre-fix the parser dropped this too because GetNextSegment("key|")   //
        // returns null when the pipe is the last character, driving the same  //
        // no-value-segment path that the else branch now handles.             //
        // -------------------------------------------------------------------- //

        /// <summary>A line with <c>key|</c> (trailing pipe, empty value token) yields one DataItem with an empty Result.</summary>
        [Test]
        public void FromString_TrailingKey_With_Empty_Value_Token_Yields_Empty_Result()
        {
            var items = ShdrDataItem.FromString($"{Timestamp}|program|").ToList();

            Assert.That(items.Count, Is.EqualTo(1),
                "'program|' MUST parse as one DataItem with an empty Result; pre-fix the parser dropped it");
            Assert.That(items[0].DataItemKey, Is.EqualTo("program"));
            Assert.That(items[0].GetValue(ValueKeys.Result), Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// A multi-pair line with an empty-value pair in the middle (<c>program||</c>) parses
        /// every pair and the middle DataItem carries an empty Result - the pre-fix corollary.
        /// </summary>
        [Test]
        public void FromString_MultiPair_With_Empty_Middle_Value_Preserves_All_Items()
        {
            var items = ShdrDataItem.FromString($"{Timestamp}|avail|AVAILABLE|program||execution|READY").ToList();

            Assert.That(items.Count, Is.EqualTo(3), "all three pairs MUST parse");
            Assert.That(items[0].DataItemKey, Is.EqualTo("avail"));
            Assert.That(items[0].GetValue(ValueKeys.Result), Is.EqualTo("AVAILABLE"));
            Assert.That(items[1].DataItemKey, Is.EqualTo("program"));
            Assert.That(items[1].GetValue(ValueKeys.Result), Is.EqualTo(string.Empty),
                "middle empty value MUST parse as empty string, not drop");
            Assert.That(items[2].DataItemKey, Is.EqualTo("execution"));
            Assert.That(items[2].GetValue(ValueKeys.Result), Is.EqualTo("READY"));
        }


        // -------------------------------------------------------------------- //
        // Baseline preservation: a concrete value MUST still round-trip        //
        // -------------------------------------------------------------------- //

        /// <summary>A concrete value on a single-pair line survives parse unchanged.</summary>
        [Test]
        public void FromString_TrailingKey_With_Concrete_Value_Preserves_Value_Verbatim()
        {
            var items = ShdrDataItem.FromString($"{Timestamp}|program|TEST.NC").ToList();

            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].DataItemKey, Is.EqualTo("program"));
            Assert.That(items[0].GetValue(ValueKeys.Result), Is.EqualTo("TEST.NC"),
                "concrete Result MUST NOT be affected by the empty-value fix");
        }
    }
}
