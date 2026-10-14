using System;
using System.Text;
using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class WavWriterTests
    {
        [Test]
        public void Encode_WritesPcmMonoHeaderAndClampedSamples()
        {
            var bytes = WavWriter.Encode(new[] { 0f, 1f, -1f, 2f }, 22050);
            Assert.AreEqual(44 + 8, bytes.Length);
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.AreEqual(36 + 8, BitConverter.ToInt32(bytes, 4));
            Assert.AreEqual("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
            Assert.AreEqual("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 20));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 22));
            Assert.AreEqual(22050, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(44100, BitConverter.ToInt32(bytes, 28));
            Assert.AreEqual(16, BitConverter.ToInt16(bytes, 34));
            Assert.AreEqual("data", Encoding.ASCII.GetString(bytes, 36, 4));
            Assert.AreEqual(8, BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 46));
            Assert.AreEqual(-32767, BitConverter.ToInt16(bytes, 48));
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 50));
        }
    }
}
