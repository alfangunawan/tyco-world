using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Ronda Malam (spec #6 §5): walk the night watch, striking the kentongan at three posts in turn. After the second
    /// post someone stands behind you on the road, and is gone when you turn around.
    /// </summary>
    public sealed class RondaMission : Mission
    {
        public const long Pay = 30000;
        public const string StrikePrompt = "[E] Pukul kentongan";
        public const float FollowerDistance = 13f, FollowerSeconds = 9f;

        readonly List<Vector3> posts = new List<Vector3>();
        readonly bool[] struck = new bool[3];
        GameObject follower;
        float followerTimer;
        bool followerSeen;

        public override string Id => "ronda";
        public override string Title => "Ronda Malam";
        public override string Detail => "Keliling ronda: pukul kentongan di tiga pos berurutan.";
        public override string RewardText => RupiahFormat.Format(Pay);
        public override float StartHour => 21f;
        public override float EndHour => 2f;
        public IReadOnlyList<Vector3> Posts => posts;
        public GameObject Follower => follower;
        public bool FollowerSeen => followerSeen;

        protected override bool Build(MissionContext c)
        {
            posts.Clear();
            for (int i = 0; i < struck.Length; i++) struck[i] = false;
            follower = null;
            followerSeen = false;
            // The baked posts, nearest first then nearest to the last; lane points fill in when fewer than three.
            var left = new List<Vector3>();
            foreach (var s in c.Landmarks(SpotKind.PosRonda)) left.Add(c.OnGround(s.position));
            var from = c.PlayerPosition;
            while (posts.Count < 3)
            {
                if (left.Count == 0)
                {
                    var p = c.LanePoint(from, 60f, 180f);
                    if (!p.HasValue) return false;
                    left.Add(p.Value);
                }
                int best = 0;
                for (int i = 1; i < left.Count; i++)
                    if (MissionContext.Flat(left[i], from) < MissionContext.Flat(left[best], from)) best = i;
                posts.Add(left[best]);
                from = left[best];
                left.RemoveAt(best);
            }
            Reward = Pay;
            for (int i = 0; i < posts.Count; i++)
            {
                int k = i;
                var prop = Own(MissionProps.Kentongan(c.Root, c.Palette));
                prop.name = "Kentongan" + (k + 1);
                prop.transform.position = posts[k];
                c.MakeTarget(prop, new Vector3(0f, 1.2f, 0.2f), new Vector3(0.6f, 2.4f, 0.9f), () => StrikePrompt, () => Strike(k), () => CanStrike(k));
                Add(new MissionStep
                {
                    Objective = "Pukul kentongan di pos " + (k + 1) + " dari 3",
                    Target = () => posts[k],
                    Done = () => struck[k]
                });
            }
            return true;
        }

        bool CanStrike(int k) => !struck[k] && (k == 0 || struck[k - 1]);

        void Strike(int k)
        {
            if (!CanStrike(k)) return;
            struck[k] = true;
            Context.Toast("TOK! TOK! TOK!");
            if (k == 1) SummonFollower();
        }

        void SummonFollower()
        {
            var c = Context;
            var behind = c.PlayerPosition - c.ViewForward * FollowerDistance;
            var at = c.LanePoint(behind, 0f, 8f) ?? c.OnGround(behind);
            follower = Own(c.Person("YangMengikuti", at, c.FacePlayer(at), PersonLook.Sosok, ghost: true));
            // Not solid: you would walk into nothing.
            var capsule = follower.GetComponent<Collider>();
            if (capsule != null) capsule.enabled = false;
            followerTimer = FollowerSeconds;
            c.Toast("Langkah kaki di belakangmu berhenti saat kamu berhenti...");
        }

        protected override void OnTick(float dt)
        {
            if (follower == null || !follower.activeSelf) return;
            followerTimer -= dt;
            var to = follower.transform.position - Context.PlayerPosition;
            to.y = 0f;
            bool looking = to.magnitude < 40f && Vector3.Dot(to.normalized, Context.ViewForward) > 0.85f;
            if (looking && !followerSeen)
            {
                followerSeen = true;
                followerTimer = Mathf.Min(followerTimer, 0.6f);
            }
            if (followerTimer > 0f) return;
            follower.SetActive(false);
            Context.Toast(followerSeen ? "...tidak ada siapa-siapa di sana." : "Langkah kaki itu hilang.");
        }

        protected override void OnSucceeded() => Context.Toast("Pak RT: \"Aman. Makasih sudah bantu ronda.\"");
    }
}
