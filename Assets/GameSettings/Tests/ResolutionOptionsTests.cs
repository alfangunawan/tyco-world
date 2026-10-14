using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.GameSettings.Tests
{
    public class ResolutionOptionsTests
    {
        static Vector2Int R(int w, int h) => new Vector2Int(w, h);

        [Test]
        public void Distinct_DropsDuplicatesAndZeros_LargestFirst()
        {
            var sizes = ResolutionOptions.Distinct(new[] { R(1280, 720), R(1920, 1080), R(1280, 720), R(0, 0), R(1920, 1080), R(1600, 900) });
            CollectionAssert.AreEqual(new[] { R(1920, 1080), R(1600, 900), R(1280, 720) }, sizes);
        }

        [Test]
        public void Labels_StartWithCurrent()
        {
            CollectionAssert.AreEqual(new[] { "Saat ini", "1920 × 1080", "1280 × 720" },
                ResolutionOptions.Labels(new[] { R(1920, 1080), R(1280, 720) }));
        }

        [Test]
        public void IndexOfAndAt_RoundTrip()
        {
            var sizes = new[] { R(1920, 1080), R(1600, 900) };
            Assert.AreEqual(2, ResolutionOptions.IndexOf(sizes, 1600, 900));
            Assert.AreEqual(R(1600, 900), ResolutionOptions.At(sizes, 2));
            Assert.AreEqual(0, ResolutionOptions.IndexOf(sizes, 0, 0));
            Assert.AreEqual(0, ResolutionOptions.IndexOf(sizes, 800, 600));
            Assert.AreEqual(Vector2Int.zero, ResolutionOptions.At(sizes, 0));
            Assert.AreEqual(Vector2Int.zero, ResolutionOptions.At(sizes, 9));
        }
    }
}
