using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;

namespace TycoWorld.WorldGen.Tests
{
    public class OverpassResponseValidatorTests
    {
        const string OneNode = "{\"version\":0.6,\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":0,\"lon\":0}]}";

        [Test]
        public void Valid_ReturnsTrue()
        {
            Assert.IsTrue(OverpassResponseValidator.Validate(OneNode, out string error));
            Assert.IsNull(error);
            Assert.AreEqual(1, OverpassResponseValidator.CountElements(OneNode));
        }

        [Test]
        public void RemarkRuntimeError_IsRejected()
        {
            const string body = "{\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":0,\"lon\":0}],\"remark\":\"runtime error: Query timed out in \\\"query\\\" at line 3 after 90 seconds.\"}";
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out string error));
            StringAssert.Contains("remark", error);
        }

        [Test]
        public void HtmlPage_IsRejected()
        {
            const string body = "<?xml version=\"1.0\"?><html><body>The server is probably too busy</body></html>";
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out _));
        }

        [Test]
        public void EmptyElements_IsRejected()
        {
            Assert.IsFalse(OverpassResponseValidator.Validate("{\"elements\":[]}", out _));
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Blank_IsRejected(string body)
        {
            Assert.IsFalse(OverpassResponseValidator.Validate(body, out _));
        }

        [Test]
        public void TruncatedJson_IsRejected()
        {
            Assert.IsFalse(OverpassResponseValidator.Validate("{\"elements\": [", out string error));
            Assert.IsNotNull(error);
        }
    }
}
