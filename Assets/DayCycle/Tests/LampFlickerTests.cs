using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.DayCycle.Tests
{
    public class LampFlickerTests
    {
        [Test]
        public void Seed_IsDeterministicPerPosition()
        {
            var a = new Vector3(-321.2f, 0f, -622.4f);
            Assert.AreEqual(LampFlicker.Seed(a), LampFlicker.Seed(a));
            Assert.AreNotEqual(LampFlicker.Seed(a), LampFlicker.Seed(a + new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void Roles_MatchFractions()
        {
            int dead = 0, flicker = 0, total = 0;
            for (int x = 0; x < 64; x++)
            for (int z = 0; z < 63; z++)
            {
                var role = LampFlicker.RoleFor(LampFlicker.Seed(new Vector3(x * 3.7f - 900f, 0f, z * 4.1f - 1300f)), 0.08f, 0.03f);
                if (role == LampRole.Dead) dead++;
                else if (role == LampRole.Flicker) flicker++;
                total++;
            }
            Assert.AreEqual(0.03f, (float)dead / total, 0.01f);
            Assert.AreEqual(0.08f, (float)flicker / total, 0.015f);
        }

        [Test]
        public void IsLit_IsDeterministic_AndHasBothStates()
        {
            uint seed = LampFlicker.Seed(new Vector3(10f, 0f, 20f));
            int lit = 0, off = 0;
            for (float t = 0f; t < 30f; t += 0.01f)
            {
                bool a = LampFlicker.IsLit(seed, t);
                Assert.AreEqual(a, LampFlicker.IsLit(seed, t));
                if (a) lit++; else off++;
            }
            Assert.Greater(lit, 0);
            Assert.Greater(off, 0);
        }

        [Test]
        public void OffGaps_AreShort()
        {
            uint seed = LampFlicker.Seed(new Vector3(-5f, 0f, 7f));
            float longest = 0f, run = 0f;
            const float step = 0.005f;
            for (float t = 0f; t < 60f; t += step)
            {
                if (LampFlicker.IsLit(seed, t)) run = 0f;
                else
                {
                    run += step;
                    longest = Mathf.Max(longest, run);
                }
            }
            Assert.LessOrEqual(longest, 0.305f);
            Assert.Greater(longest, 0.04f);
        }
    }
}
