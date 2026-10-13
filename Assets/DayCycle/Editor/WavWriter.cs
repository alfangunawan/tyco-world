using System;
using System.IO;
using System.Text;

namespace TycoWorld.DayCycle.Authoring
{
    /// <summary>16-bit PCM mono WAV encoder for the synthesized ambience.</summary>
    public static class WavWriter
    {
        public static byte[] Encode(float[] samples, int sampleRate)
        {
            int dataBytes = samples.Length * 2;
            using (var stream = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var s in samples)
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, s)) * 32767f));
                w.Flush();
                return stream.ToArray();
            }
        }
    }
}
