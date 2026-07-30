// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.SparkplugB.Transport;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace MTConnect.SparkplugB.Tests
{
    /// <summary>
    /// Pins the Sparkplug topic namespace. Group, Edge Node and Device IDs become topic levels
    /// verbatim, and the STATE topic is shaped differently from every other one, so mistakes here
    /// are silent rather than loud — a malformed STATE topic simply means the Edge Node never hears
    /// the primary host.
    /// </summary>
    [TestFixture]
    public class SparkplugTopicTests
    {
        [Test]
        public void Node_topics_have_four_levels()
        {
            Assert.That(SparkplugTopic.Node("Factory", SparkplugMessageType.NBIRTH, "Agent1"),
                Is.EqualTo("spBv1.0/Factory/NBIRTH/Agent1"));
            Assert.That(SparkplugTopic.Node("Factory", SparkplugMessageType.NDEATH, "Agent1"),
                Is.EqualTo("spBv1.0/Factory/NDEATH/Agent1"));
        }

        [Test]
        public void Device_topics_append_the_device_id()
        {
            Assert.That(SparkplugTopic.Device("Factory", SparkplugMessageType.DBIRTH, "Agent1", "Machine-01"),
                Is.EqualTo("spBv1.0/Factory/DBIRTH/Agent1/Machine-01"));
        }

        [Test]
        public void State_topic_carries_no_group_segment()
        {
            // The one asymmetry in the namespace: STATE is scoped to the host, not the group.
            Assert.That(SparkplugTopic.State("SCADA-1"), Is.EqualTo("spBv1.0/STATE/SCADA-1"));
        }

        [TestCase("Machine-01", true)]
        [TestCase("Agent1", true)]
        [TestCase("has/slash", false)]      // would fabricate an extra topic level
        [TestCase("has+plus", false)]       // collides with a single-level wildcard
        [TestCase("has#hash", false)]       // collides with a multi-level wildcard
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("   ", false)]
        public void Identifiers_that_would_reshape_the_namespace_are_rejected(string id, bool expected)
        {
            Assert.That(SparkplugTopic.IsValidIdentifier(id), Is.EqualTo(expected));
        }

        [Test]
        public void Device_topics_round_trip_through_the_parser()
        {
            var ok = SparkplugTopic.TryParse("spBv1.0/Factory/DDATA/Agent1/Machine-01",
                out var group, out var type, out var node, out var device);

            Assert.That(ok, Is.True);
            Assert.That(group, Is.EqualTo("Factory"));
            Assert.That(type, Is.EqualTo(SparkplugMessageType.DDATA));
            Assert.That(node, Is.EqualTo("Agent1"));
            Assert.That(device, Is.EqualTo("Machine-01"));
        }

        [Test]
        public void Node_topics_parse_with_no_device()
        {
            var ok = SparkplugTopic.TryParse("spBv1.0/Factory/NCMD/Agent1",
                out _, out var type, out _, out var device);

            Assert.That(ok, Is.True);
            Assert.That(type, Is.EqualTo(SparkplugMessageType.NCMD));
            Assert.That(device, Is.Null);
        }

        [TestCase("spBv1.0/STATE/SCADA-1")]          // STATE is not a group-scoped topic
        [TestCase("spBv1.0/Factory/BOGUS/Agent1")]   // unknown message type
        [TestCase("other/Factory/NBIRTH/Agent1")]    // wrong namespace
        [TestCase("spBv1.0/Factory/NBIRTH")]         // too few levels
        [TestCase("")]
        public void Non_sparkplug_topics_are_rejected(string topic)
        {
            Assert.That(SparkplugTopic.TryParse(topic, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void State_host_id_is_extracted_only_from_state_topics()
        {
            Assert.That(SparkplugTopic.TryParseStateHostId("spBv1.0/STATE/SCADA-1"), Is.EqualTo("SCADA-1"));
            Assert.That(SparkplugTopic.TryParseStateHostId("spBv1.0/Factory/NBIRTH/Agent1"), Is.Null);
            Assert.That(SparkplugTopic.TryParseStateHostId("spBv1.0/STATE/"), Is.Null);
        }
    }

    /// <summary>
    /// Pins the message sequence number. A Host Application uses it to detect loss, so an off-by-one
    /// or a missed wrap costs the entire Edge Node a rebirth.
    /// </summary>
    [TestFixture]
    public class SparkplugSequenceTests
    {
        [Test]
        public void Sequence_starts_at_zero_so_NBIRTH_carries_zero()
        {
            var seq = new SparkplugSequence();
            Assert.That(seq.Next(), Is.EqualTo(0UL));
        }

        [Test]
        public void Sequence_increments_by_exactly_one()
        {
            var seq = new SparkplugSequence();
            var taken = Enumerable.Range(0, 5).Select(_ => seq.Next()).ToArray();
            Assert.That(taken, Is.EqualTo(new ulong[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void Sequence_wraps_255_back_to_zero()
        {
            var seq = new SparkplugSequence();
            for (var i = 0; i < 255; i++) seq.Next();

            Assert.That(seq.Next(), Is.EqualTo(255UL), "255 is a legal sequence number");
            Assert.That(seq.Next(), Is.EqualTo(0UL), "256 must wrap to 0, not overflow");
        }

        [Test]
        public void Reset_restarts_the_session_at_zero()
        {
            var seq = new SparkplugSequence();
            seq.Next();
            seq.Next();

            seq.Reset();

            Assert.That(seq.Next(), Is.EqualTo(0UL));
        }
    }

    /// <summary>
    /// Pins bdSeq, which is how a Host Application tells a live session from a stale NDEATH that the
    /// broker delivered late.
    /// </summary>
    [TestFixture]
    public class SparkplugBirthDeathSequenceTests
    {
        private string _statePath;

        [SetUp]
        public void CreateStatePath() =>
            _statePath = Path.Combine(Path.GetTempPath(), $"bdseq-{Guid.NewGuid():N}.txt");

        [TearDown]
        public void RemoveStatePath()
        {
            try { if (File.Exists(_statePath)) File.Delete(_statePath); } catch { }
        }

        [Test]
        public void First_connection_uses_bdSeq_zero()
        {
            // "MUST start at zero" — Current is what the first CONNECT's will and NBIRTH carry.
            Assert.That(new SparkplugBirthDeathSequence().Current, Is.EqualTo(0L));
        }

        [Test]
        public void Each_connection_advances_by_one()
        {
            var bdSeq = new SparkplugBirthDeathSequence();
            Assert.That(bdSeq.Advance(), Is.EqualTo(1L));
            Assert.That(bdSeq.Advance(), Is.EqualTo(2L));
            Assert.That(bdSeq.Current, Is.EqualTo(2L));
        }

        [Test]
        public void bdSeq_wraps_255_back_to_zero()
        {
            var bdSeq = new SparkplugBirthDeathSequence();
            for (var i = 0; i < 255; i++) bdSeq.Advance();

            Assert.That(bdSeq.Current, Is.EqualTo(255L));
            Assert.That(bdSeq.Advance(), Is.EqualTo(0L));
        }

        [Test]
        public void bdSeq_survives_a_process_restart()
        {
            // The reason persistence exists: a restarted node beginning again at zero would replay
            // values the host has already seen, so a new session could look like the stale one.
            var first = new SparkplugBirthDeathSequence(_statePath);
            first.Advance();
            first.Advance();

            var afterRestart = new SparkplugBirthDeathSequence(_statePath);

            Assert.That(afterRestart.Current, Is.EqualTo(2L));
            Assert.That(afterRestart.Advance(), Is.EqualTo(3L));
        }

        [Test]
        public void A_corrupt_store_starts_from_zero_and_reports_rather_than_throwing()
        {
            File.WriteAllText(_statePath, "not-a-number");
            string reported = null;

            var bdSeq = new SparkplugBirthDeathSequence(_statePath);
            bdSeq.PersistenceFailed += reason => reported = reason;

            // Constructed without throwing; the relay must start even with an unusable store.
            Assert.That(bdSeq.Current, Is.EqualTo(0L));
            Assert.That(bdSeq.Advance(), Is.EqualTo(1L));
            Assert.That(reported, Is.Null, "the load already happened in the constructor");
        }

        [Test]
        public void An_out_of_range_stored_value_is_ignored()
        {
            File.WriteAllText(_statePath, "9999");
            Assert.That(new SparkplugBirthDeathSequence(_statePath).Current, Is.EqualTo(0L));
        }

        [Test]
        public void No_state_path_keeps_the_counter_in_memory()
        {
            var bdSeq = new SparkplugBirthDeathSequence(null);
            Assert.That(bdSeq.Advance(), Is.EqualTo(1L));
        }
    }
}
