using System.Collections.Generic;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class NoRepeatPickerTests
    {
        [Test]
        public void Next_NeverRepeatsAndCoversAll()
        {
            var picker = new NoRepeatPicker(42u);
            var seen = new HashSet<int>();
            int last = -1;
            for (int i = 0; i < 1000; i++)
            {
                int pick = picker.Next(4);
                Assert.That(pick, Is.InRange(0, 3));
                Assert.AreNotEqual(last, pick);
                seen.Add(pick);
                last = pick;
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new NoRepeatPicker(7u);
            var b = new NoRepeatPicker(7u);
            for (int i = 0; i < 50; i++) Assert.AreEqual(a.Next(5), b.Next(5));
        }

        [Test]
        public void EdgeCounts()
        {
            var p = new NoRepeatPicker(1u);
            Assert.AreEqual(-1, p.Next(0));
            Assert.AreEqual(0, p.Next(1));
            Assert.AreEqual(0, p.Next(1));
        }

        [Test]
        public void Range_StaysInside()
        {
            var p = new NoRepeatPicker(0u);
            for (int i = 0; i < 200; i++) Assert.That(p.Range(0.9f, 1.1f), Is.InRange(0.9f, 1.1f));
        }
    }
}
