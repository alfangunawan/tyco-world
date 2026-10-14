using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Suara dari Kuburan (spec #6 §5, story, once): warga hear a voice from the cemetery at night. Find the old radio
    /// by its static, switch it off, see who was listening, and go home with the sticker on its back.
    /// </summary>
    public sealed class KuburanMission : Mission
    {
        public const long Pay = 40000;
        public const float ArriveRadius = 14f, RevealWithin = 22f, StaticWithin = 40f, StaticEvery = 5f;
        public const string RadioPrompt = "[E] Matikan radio";
        public const string Story = "Di balik radio ada stiker pudar: \"Kos Raflesia, kamar 3\". Kata ibu kos, kamar itu kosong sejak 2009.";

        GameObject radio, watcher;
        Vector3 cemetery, radioAt, hint, kos;
        bool off, heardClose;
        float staticTimer, watcherTimer;

        public override string Id => "kuburan";
        public override string Title => "Suara dari Kuburan";
        public override string Detail => "Warga dengar suara orang bicara dari arah kuburan tiap malam. Cari sumbernya.";
        public override string RewardText => RupiahFormat.Format(Pay) + " + cerita";
        public override float StartHour => 22f;
        public override float EndHour => 4f;
        public override bool Repeatable => false;
        public GameObject Radio => radio;
        public GameObject Watcher => watcher;
        public Vector3 RadioPosition => radioAt;

        protected override bool Build(MissionContext c)
        {
            off = heardClose = false;
            staticTimer = 0f;
            watcher = null;
            var grave = c.NearestLandmark(SpotKind.Cemetery, c.PlayerPosition);
            var home = c.NearestLandmark(SpotKind.Kos, c.PlayerPosition);
            if (grave == null || home == null) return false;
            cemetery = c.OnGround(grave.position);
            kos = c.OnGround(home.position);
            var spot = c.LanePoint(cemetery, 8f, 26f);
            if (!spot.HasValue) return false;
            radioAt = spot.Value;
            float angle = c.Range(0f, Mathf.PI * 2f);
            hint = radioAt + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * c.Range(6f, 14f);
            Reward = Pay;
            radio = Own(MissionProps.Radio(c.Root, c.Palette));
            radio.transform.SetPositionAndRotation(radioAt, Quaternion.Euler(0f, c.Range(0f, 360f), 0f));
            c.MakeTarget(radio, new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.4f, 0.3f), () => RadioPrompt, SwitchOff, () => !off);
            Add(new MissionStep { Objective = "Datangi kuburan", Target = () => cemetery, Done = () => c.Near(cemetery, ArriveRadius) });
            Add(new MissionStep
            {
                Objective = "Cari sumber suara",
                Target = () => c.Near(radioAt, RevealWithin) ? radioAt : hint,
                RevealWithin = RevealWithin,
                Enter = () => c.Toast("Ada suara laki-laki membaca nama-nama... dari radio?"),
                Done = () => off
            });
            Add(new MissionStep { Objective = "Pulang ke kos", Target = () => kos, Done = () => c.Near(kos, 8f) });
            return true;
        }

        void SwitchOff()
        {
            if (off) return;
            off = true;
            var c = Context;
            c.Toast("Radio mati. Sunyi. Lalu kamu sadar ada yang ikut mendengarkan.");
            // Someone stands just past the radio, facing you, for a moment.
            var away = radioAt - c.PlayerPosition;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : c.ViewForward;
            var at = c.OnGround(radioAt + away * 4.5f);
            watcher = Own(c.Person("YangMendengarkan", at, -away, PersonLook.Sosok, ghost: true));
            var capsule = watcher.GetComponent<Collider>();
            if (capsule != null) capsule.enabled = false;
            watcherTimer = 2.5f;
            if (radio != null) radio.SetActive(false);
        }

        protected override void OnTick(float dt)
        {
            if (watcher != null && watcher.activeSelf)
            {
                watcherTimer -= dt;
                if (watcherTimer <= 0f)
                {
                    watcher.SetActive(false);
                    Context.Toast(Story);
                }
            }
            if (off || StepIndex < 1) return;
            staticTimer -= dt;
            bool close = Context.Near(radioAt, 12f);
            // Stepping close the voice turns clear at once; further out the static repeats.
            if (close && !heardClose) staticTimer = 0f;
            if (staticTimer > 0f || !Context.Near(radioAt, StaticWithin)) return;
            staticTimer = StaticEvery;
            heardClose |= close;
            Context.Toast(close ? "\"...kamar tiga... kamar tiga...\"" : "kresek... kresek... (suara radio)");
        }

        protected override void OnSucceeded() => Context.Toast("Kamu tidak bisa tidur malam ini. Radio itu kamu tinggal di kuburan.");
    }
}
