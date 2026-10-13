using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class FetcherTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "tyco_wg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void CreateRequest_SetsUserAgentAndPost()
        {
            using (var request = OsmFetcher.CreateRequest("https://example.invalid/api/interpreter", "[out:json];node(1);out;", 90))
            {
                Assert.AreEqual("POST", request.method);
                Assert.AreEqual(OsmFetcher.UserAgent, request.GetRequestHeader("User-Agent"));
                Assert.AreEqual(90, request.timeout);
                Assert.AreEqual("https://example.invalid/api/interpreter", request.url);
                string body = Encoding.UTF8.GetString(request.uploadHandler.data);
                StringAssert.StartsWith("data=", body);
                Assert.AreEqual("[out:json];node(1);out;", Uri.UnescapeDataString(body.Substring(5).Replace('+', ' ')));
            }
        }

        [Test]
        public void WriteAtomic_ReplacesContentAndLeavesNoTemp()
        {
            string path = Path.Combine(dir, "sub", "osm_raw.json");
            CacheWriter.WriteAtomic(path, "first");
            CacheWriter.WriteAtomic(path, "second");
            Assert.AreEqual("second", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void InfoPath_SitsNextToRawFile()
        {
            Assert.AreEqual(Path.Combine(dir, "osm_raw.info.json"), CacheWriter.InfoPath(Path.Combine(dir, "osm_raw.json")));
        }

        [Test]
        public void WriteInfo_IsCultureInvariant()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                string raw = Path.Combine(dir, "osm_raw.json");
                File.WriteAllText(raw, "{}");
                CacheWriter.WriteInfo(raw, "https://overpass-api.de/api/interpreter",
                    new GeoBounds(-6.9850, 107.6255, -6.9675, 107.6380), 5, new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc));
                string info = File.ReadAllText(CacheWriter.InfoPath(raw));
                StringAssert.Contains("-6.985", info);
                StringAssert.DoesNotContain("-6,985", info);
                StringAssert.Contains("5 elemen", CacheWriter.ReadInfoSummary(raw));
                StringAssert.Contains("2026-10-13T01:02:03", CacheWriter.ReadInfoSummary(raw));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ReadInfoSummary_NoData()
        {
            StringAssert.StartsWith("Belum ada data", CacheWriter.ReadInfoSummary(Path.Combine(dir, "missing.json")));
        }
    }
}
