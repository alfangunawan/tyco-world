using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Kucing Hilang (spec #6 §5): an ibu at her gate lost her ginger cat. It hides by a lane somewhere around; the
    /// beacon only shows within 30 m, so you follow the meowing, pick it up and bring it home.
    /// </summary>
    public sealed class KucingMission : Mission
    {
        public const long Pay = 20000;
        public const float OwnerMin = 50f, OwnerMax = 260f, CatMin = 45f, CatMax = 140f;
        public const float RevealWithin = 30f, MeowWithin = 45f, MeowEvery = 6f;
        public const string AskPrompt = "[E] Tanya ibu pemilik kucing", CatchPrompt = "[E] Tangkap kucing", ReturnPrompt = "[E] Serahkan kucing";

        GameObject owner, cat;
        Vector3 catAt, hint;
        bool asked, caught, returned;
        float meowTimer;

        public override string Id => "kucing";
        public override string Title => "Kucing Hilang";
        public override string Detail => "Kucing oren Bu Yati kabur. Cari dari suara meongnya dan bawa pulang.";
        public override string RewardText => RupiahFormat.Format(Pay);
        public override float StartHour => 7f;
        public override float EndHour => 17f;
        public GameObject Owner => owner;
        public GameObject Cat => cat;
        public Vector3 CatPosition => catAt;

        protected override bool Build(MissionContext c)
        {
            asked = caught = returned = false;
            meowTimer = 0f;
            var door = c.Door(c.PlayerPosition, OwnerMin, OwnerMax);
            if (door == null) return false;
            var home = c.InFrontOf(door);
            var spot = c.LanePoint(home, CatMin, CatMax);
            if (!spot.HasValue) return false;
            catAt = spot.Value;
            // The search hint: somewhere within 25 m of the cat, so the compass gives a rough direction only.
            float angle = c.Range(0f, Mathf.PI * 2f);
            hint = catAt + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * c.Range(10f, 25f);
            Reward = Pay;
            var look = PersonLook.From((ulong)c.Range(1, 1 << 20));
            look.Hijab = true;
            var face = Quaternion.Euler(0f, door.yaw, 0f) * Vector3.forward;
            owner = Own(c.Person("BuYati", home, face, look, () => caught ? ReturnPrompt : AskPrompt, Talk, () => !asked || (caught && !returned)));
            cat = Own(MissionProps.Cat(c.Root, c.Palette));
            cat.transform.SetPositionAndRotation(catAt, Quaternion.Euler(0f, c.Range(0f, 360f), 0f));
            c.MakeTarget(cat, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.6f), () => CatchPrompt, Catch, () => asked && !caught);
            Add(new MissionStep { Objective = "Temui Bu Yati di depan rumahnya", Target = () => home, Done = () => asked });
            Add(new MissionStep
            {
                Objective = "Cari kucing oren (dengarkan suara meongnya)",
                Target = () => c.Near(catAt, RevealWithin) ? catAt : hint,
                RevealWithin = RevealWithin,
                Done = () => caught
            });
            Add(new MissionStep { Objective = "Kembalikan kucing ke Bu Yati", Target = () => home, Done = () => returned });
            return true;
        }

        void Talk()
        {
            if (!asked)
            {
                asked = true;
                Context.Toast("Bu Yati: \"Si Oyen kabur dari pagi. Biasanya ngumpet di pinggir jalan sekitar sini.\"");
            }
            else if (caught && !returned)
            {
                returned = true;
                Context.Toast("Bu Yati: \"Ya ampun Oyen! Makasih ya, nak.\"");
            }
        }

        void Catch()
        {
            if (caught) return;
            caught = true;
            if (cat != null) cat.SetActive(false);
            Context.Toast("Kamu menggendong si Oyen. Dia mendengkur.");
        }

        protected override void OnTick(float dt)
        {
            if (!asked || caught) return;
            meowTimer -= dt;
            if (meowTimer > 0f || !Context.Near(catAt, MeowWithin)) return;
            meowTimer = MeowEvery;
            Context.Toast(Context.Near(catAt, 15f) ? "\"MEONG!\" (dekat sekali)" : "\"meong...\" (terdengar dari dekat sini)");
        }
    }
}
