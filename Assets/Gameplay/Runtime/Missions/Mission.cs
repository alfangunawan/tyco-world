using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    public enum MissionStatus
    {
        Idle,
        Running,
        Succeeded,
        Failed
    }

    /// <summary>
    /// One step of a mission (spec #6 §2 "Misi"): what the HUD says, where the beacon stands (optional, may move), and
    /// when the step is done. Enter runs once when the step starts; a time limit fails the mission when it runs out.
    /// </summary>
    public sealed class MissionStep
    {
        public string Objective = "";
        public Func<Vector3?> Target;
        public Func<bool> Done;
        public Action Enter;
        public float TimeLimit = -1f;
        /// <summary>Beacon and compass hide until the player is this close (a search, not a waypoint); 0 always shows.</summary>
        public float RevealWithin;

        public static MissionStep GoTo(string objective, Vector3 target, Func<bool> done, float timeLimit = -1f) =>
            new MissionStep { Objective = objective, Target = () => target, Done = done, TimeLimit = timeLimit };
    }

    /// <summary>
    /// A side job: a fixed id and listing text, an hour window to start it, and a run of steps built on Start from the
    /// world around the player. Subclasses build the steps and decide the reward; the base runs them in order.
    /// </summary>
    public abstract class Mission
    {
        readonly List<MissionStep> steps = new List<MissionStep>();
        readonly List<GameObject> spawned = new List<GameObject>();
        int index;
        float stepTime;

        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Detail { get; }
        public abstract string RewardText { get; }
        /// <summary>Start hour (inclusive) and end hour (exclusive) the job is offered; End &lt; Start wraps midnight.</summary>
        public abstract float StartHour { get; }
        public abstract float EndHour { get; }
        /// <summary>Story jobs are done once; errands can be taken again.</summary>
        public virtual bool Repeatable => true;

        public MissionStatus Status { get; private set; }
        public MissionContext Context { get; private set; }
        public string FailReason { get; private set; } = "";
        public long Reward { get; protected set; }
        public int StepIndex => index;
        public int StepCount => steps.Count;
        public MissionStep Current => Status == MissionStatus.Running && index < steps.Count ? steps[index] : null;
        public string Objective => Current != null ? Current.Objective : "";
        public float TimeLeft => Current != null && Current.TimeLimit > 0f ? Mathf.Max(0f, Current.TimeLimit - stepTime) : -1f;

        public Vector3? Target
        {
            get
            {
                var step = Current;
                return step != null && step.Target != null ? step.Target() : null;
            }
        }

        public bool InWindow(float hours)
        {
            float h = ((hours % 24f) + 24f) % 24f;
            return StartHour <= EndHour ? h >= StartHour && h < EndHour : h >= StartHour || h < EndHour;
        }

        public string WindowText => Hour(StartHour) + "-" + Hour(EndHour);

        static string Hour(float h) => Mathf.FloorToInt(((h % 24f) + 24f) % 24f).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".00";

        /// <summary>Builds the steps around the player; false (with a toast reason) when the world has no fitting place.</summary>
        public bool Start(MissionContext context)
        {
            Context = context;
            steps.Clear();
            spawned.Clear();
            index = 0;
            stepTime = 0f;
            Reward = 0;
            FailReason = "";
            if (!Build(context))
            {
                Cleanup();
                Status = MissionStatus.Idle;
                return false;
            }
            Status = MissionStatus.Running;
            EnterStep();
            return true;
        }

        protected abstract bool Build(MissionContext context);

        protected void Add(MissionStep step) => steps.Add(step);

        /// <summary>Tracks an object the mission made; all of them go when it ends.</summary>
        protected T Own<T>(T obj) where T : UnityEngine.Object
        {
            if (obj is GameObject go) spawned.Add(go);
            else if (obj is Component c) spawned.Add(c.gameObject);
            return obj;
        }

        public void Tick(float dt)
        {
            if (Status != MissionStatus.Running) return;
            OnTick(dt);
            if (Status != MissionStatus.Running) return;
            var step = steps[index];
            stepTime += dt;
            if (step.Done != null && step.Done())
            {
                index++;
                stepTime = 0f;
                if (index >= steps.Count)
                {
                    Status = MissionStatus.Succeeded;
                    OnSucceeded();
                    Cleanup();
                    return;
                }
                EnterStep();
                return;
            }
            if (step.TimeLimit > 0f && stepTime >= step.TimeLimit) Fail(TimeoutText);
        }

        protected virtual string TimeoutText => "Waktunya habis.";

        protected virtual void OnTick(float dt)
        {
        }

        protected virtual void OnSucceeded()
        {
        }

        protected virtual void OnFailed()
        {
        }

        void EnterStep() => steps[index].Enter?.Invoke();

        public void Fail(string reason)
        {
            if (Status != MissionStatus.Running) return;
            FailReason = reason ?? "";
            Status = MissionStatus.Failed;
            OnFailed();
            Cleanup();
        }

        void Cleanup()
        {
            foreach (var go in spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
            spawned.Clear();
            OnCleanup();
        }

        protected virtual void OnCleanup()
        {
        }
    }
}
