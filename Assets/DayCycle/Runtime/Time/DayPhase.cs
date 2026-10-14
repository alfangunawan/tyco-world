using System;

namespace TycoWorld.DayCycle
{
    public enum DayPhaseKind { Dawn, Day, Dusk, Night }

    /// <summary>Phase of the day and the horror window (19:00–05:00); events fire once per transition, also on time jumps.</summary>
    public sealed class DayPhase
    {
        public event Action HorrorStarted;
        public event Action HorrorEnded;

        public DayPhaseKind Phase { get; private set; }
        public bool IsHorror { get; private set; }

        public static DayPhaseKind PhaseAt(float hours)
        {
            if (hours >= DayTimes.NightStart || hours < DayTimes.DawnStart) return DayPhaseKind.Night;
            if (hours < DayTimes.DayStart) return DayPhaseKind.Dawn;
            return hours < DayTimes.DuskStart ? DayPhaseKind.Day : DayPhaseKind.Dusk;
        }

        public static bool HorrorAt(float hours) => hours >= DayTimes.HorrorStart || hours < DayTimes.HorrorEnd;

        public void Initialize(float hours)
        {
            Phase = PhaseAt(hours);
            IsHorror = HorrorAt(hours);
        }

        public void Update(float hours)
        {
            Phase = PhaseAt(hours);
            bool horror = HorrorAt(hours);
            if (horror == IsHorror) return;
            IsHorror = horror;
            if (horror) HorrorStarted?.Invoke();
            else HorrorEnded?.Invoke();
        }
    }
}
