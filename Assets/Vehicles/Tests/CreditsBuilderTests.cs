using System.IO;
using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Every third-party asset is credited in the game (spec vehicles-3a §2).</summary>
    public class CreditsBuilderTests
    {
        [Test]
        public void Credits_ListEveryRowOfEverySourcesFile()
        {
            string text = CreditsBuilder.Collect();
            var files = Directory.GetFiles("Assets", "SOURCES.md", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            int rows = 0;
            foreach (var file in files)
                foreach (var cells in CreditsBuilder.Rows(File.ReadAllLines(file)))
                {
                    // Sounds this project synthesised itself are not third-party.
                    if (cells.Any(c => c.Contains(CreditsBuilder.OwnWork) || c.StartsWith(CreditsBuilder.Rejected))) continue;
                    StringAssert.Contains(CreditsBuilder.Plain(cells[1]), text, file);
                    rows++;
                }
            Assert.Greater(rows, 20, "the vehicle, character, barrier, texture and sound rows");
            StringAssert.Contains("Han66st", text);
            StringAssert.Contains("CC Attribution 4.0", text);
            StringAssert.Contains("ambientcg.com", text);
            StringAssert.DoesNotContain("](", text, "markdown links are written out plainly");
            StringAssert.DoesNotContain(CreditsBuilder.Rejected, text, "candidates that were turned down are not credited");
            var lines = text.Split('\n').Where(l => l.StartsWith("  • ")).ToList();
            CollectionAssert.AllItemsAreUnique(lines, "one line per asset");
        }

        [Test]
        public void Plain_WritesLinksAndCodeOut()
        {
            Assert.AreEqual("Universal Animation Library (https://opengameart.org/x)", CreditsBuilder.Plain("[Universal Animation Library](https://opengameart.org/x)"));
            Assert.AreEqual("vario.glb", CreditsBuilder.Plain("`vario.glb`"));
        }

        [Test]
        public void WrittenFile_MatchesTheCollection()
        {
            Assert.AreEqual(CreditsBuilder.Collect(), File.ReadAllText(CreditsBuilder.OutputPath));
        }
    }
}
