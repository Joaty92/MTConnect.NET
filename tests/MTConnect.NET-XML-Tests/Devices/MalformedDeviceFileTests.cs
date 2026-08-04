// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using MTConnect.Configurations;
using NUnit.Framework;

namespace MTConnect.Tests.Xml.Devices
{
    /// <summary>
    /// Pins that a malformed Device configuration file fails fast, in isolation, and visibly.
    ///
    /// A stray character inside an attribute - the kind a careless find/replace produces -
    /// sent agent startup into a 100% CPU spin: no Devices loaded at all, no log output, and
    /// startup never completed. The spin needed the malformed attribute to sit inside a nested
    /// <c>Components</c> tree: <c>XmlComponentCollection.ReadXml</c> swallowed the XmlException,
    /// leaving the reader parked on the same node, and the generated XmlSerializer re-entered it
    /// forever. A flat fragment merely failed to load.
    ///
    /// These tests are time-bounded: a spin fails them by timeout rather than hanging the run.
    ///
    /// The fixture lives in the XML test project because <see cref="DeviceConfiguration"/>
    /// resolves its formatter by id at runtime; without a reference to MTConnect.NET-XML the
    /// "xml" formatter is not registered and every load silently yields nothing.
    /// </summary>
    [TestFixture]
    public class MalformedDeviceFileTests
    {
        private const string Good =
            "<Device id=\"dev1\" name=\"dev1\" uuid=\"dev1\">\n" +
            "  <DataItems>\n" +
            "    <DataItem category=\"EVENT\" id=\"dev1_avail\" name=\"avail\" type=\"AVAILABILITY\"/>\n" +
            "  </DataItems>\n" +
            "</Device>\n";

        // Stray ']' before '=', nested inside Components - the combination that spun.
        private const string Malformed =
            "<Device id=\"dev2\" name=\"dev2\" uuid=\"dev2\">\n" +
            "  <Components>\n" +
            "    <Controller id=\"dev2_controller\" name=\"Controller\">\n" +
            "      <Components>\n" +
            "        <Path id=\"dev2_path\" name=\"Path\">\n" +
            "          <DataItems>\n" +
            "            <DataItem category=\"SAMPLE\" id=\"dev2_len\" name=\"len\" type=\"LENGTH\" subType]=\"PROGRAMMED\"/>\n" +
            "          </DataItems>\n" +
            "        </Path>\n" +
            "      </Components>\n" +
            "    </Controller>\n" +
            "  </Components>\n" +
            "</Device>\n";

        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "mtc-malformed-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>Guards the fixture itself: a valid file must load, or the rest proves nothing.</summary>
        [Test]
        [Timeout(10000)]
        public void FromFiles_loads_a_valid_device_file()
        {
            File.WriteAllText(Path.Combine(_dir, "Good.xml"), Good);

            var result = DeviceConfiguration.FromFiles(_dir, "xml");

            Assert.That(result, Is.Not.Null.And.Not.Empty, "Baseline: a valid device file must load.");
            Assert.That(result.Select(o => o.Uuid), Does.Contain("dev1"));
        }

        /// <summary>Pins that a malformed nested file returns instead of spinning.</summary>
        [Test]
        [Timeout(10000)]
        public void FromFile_returns_for_a_malformed_nested_file_instead_of_spinning()
        {
            var path = Path.Combine(_dir, "Bad.xml");
            File.WriteAllText(path, Malformed);

            var result = DeviceConfiguration.FromFile(path, "xml");

            Assert.That(result, Is.Null.Or.Empty, "A malformed file must yield no devices.");
        }

        /// <summary>Pins that one malformed file does not stop its siblings from loading.</summary>
        [Test]
        [Timeout(10000)]
        public void FromFiles_still_loads_the_good_files_alongside_a_malformed_one()
        {
            // "Bad" sorts before "Good", so the failure is encountered first.
            File.WriteAllText(Path.Combine(_dir, "Bad.xml"), Malformed);
            File.WriteAllText(Path.Combine(_dir, "Good.xml"), Good);

            var result = DeviceConfiguration.FromFiles(_dir, "xml");

            Assert.That(result, Is.Not.Null, "A malformed sibling must not abort the whole directory scan.");
            Assert.That(result.Select(o => o.Uuid), Does.Contain("dev1"),
                "The valid device must still load when a malformed file is present.");
        }

        /// <summary>Pins that the unreadable file is reported by name so it can be logged.</summary>
        [Test]
        [Timeout(10000)]
        public void FromFiles_reports_the_unreadable_file_by_path()
        {
            var badPath = Path.Combine(_dir, "Bad.xml");
            File.WriteAllText(badPath, Malformed);
            File.WriteAllText(Path.Combine(_dir, "Good.xml"), Good);

            DeviceConfiguration.FromFiles(_dir, "xml", out var unreadable);

            Assert.That(unreadable, Is.Not.Null.And.Not.Empty,
                "A file that produced no Device must be reported, not skipped silently.");
            Assert.That(unreadable, Does.Contain(badPath));
            Assert.That(unreadable.Count(), Is.EqualTo(1), "The valid file must not be reported as unreadable.");
        }
    }
}