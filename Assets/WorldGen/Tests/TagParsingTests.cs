using System.Globalization;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class TagParsingTests
    {
        [TestCase("6", 6f)]
        [TestCase("6 m", 6f)]
        [TestCase("6.5", 6.5f)]
        [TestCase("6,5", 6f)]
        [TestCase("10;12", 10f)]
        [TestCase(" 4.25m ", 4.25f)]
        [TestCase(".5", 0.5f)]
        public void TryParseLeadingFloat_Valid(string text, float expected)
        {
            Assert.IsTrue(TagParsing.TryParseLeadingFloat(text, out float value));
            Assert.AreEqual(expected, value, 1e-5f);
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("abc")]
        [TestCase("-2")]
        [TestCase("0")]
        [TestCase(".")]
        public void TryParseLeadingFloat_Invalid(string text)
        {
            Assert.IsFalse(TagParsing.TryParseLeadingFloat(text, out _));
        }

        [TestCase("4", 4)]
        [TestCase("2.5", 3)]
        [TestCase("20", 20)]
        [TestCase("1;2", 1)]
        public void TryParseLevels_Valid(string text, int expected)
        {
            Assert.IsTrue(TagParsing.TryParseLevels(text, out int levels));
            Assert.AreEqual(expected, levels);
        }

        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("500")]
        public void TryParseLevels_Invalid(string text)
        {
            Assert.IsFalse(TagParsing.TryParseLevels(text, out _));
        }

        [Test]
        public void Parsing_IsCultureInvariant_UnderIndonesianLocale()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("id-ID");
                Assert.IsTrue(TagParsing.TryParseLeadingFloat("6.5", out float value));
                Assert.AreEqual(6.5f, value, 1e-5f);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
