using System;

namespace djack.RogueSurvivor.Data
{
    // The first word retains the existing save masks; extra words are stored only when used.
    [Serializable]
    struct NpcPlanningState : IEquatable<NpcPlanningState>
    {
        public ulong Low, A, B, C;
        public bool Extended { get { return (A | B | C) != 0; } }
        public bool Empty { get { return (Low | A | B | C) == 0; } }
        public static implicit operator NpcPlanningState(ulong low) { return new NpcPlanningState { Low = low }; }
        public static NpcPlanningState Bit(int index)
        {
            if (index < 0 || index >= 256) throw new ArgumentOutOfRangeException("index");
            ulong bit = 1UL << (index % 64);
            return index < 64 ? new NpcPlanningState { Low = bit } : index < 128 ? new NpcPlanningState { A = bit } :
                index < 192 ? new NpcPlanningState { B = bit } : new NpcPlanningState { C = bit };
        }
        public bool Contains(NpcPlanningState required) { return (this & required).Equals(required); }
        public static NpcPlanningState operator &(NpcPlanningState a, NpcPlanningState b)
        { return new NpcPlanningState { Low = a.Low & b.Low, A = a.A & b.A, B = a.B & b.B, C = a.C & b.C }; }
        public static NpcPlanningState operator |(NpcPlanningState a, NpcPlanningState b)
        { return new NpcPlanningState { Low = a.Low | b.Low, A = a.A | b.A, B = a.B | b.B, C = a.C | b.C }; }
        public static NpcPlanningState operator ~(NpcPlanningState a)
        { return new NpcPlanningState { Low = ~a.Low, A = ~a.A, B = ~a.B, C = ~a.C }; }
        public bool Equals(NpcPlanningState other) { return Low == other.Low && A == other.A && B == other.B && C == other.C; }
        public override bool Equals(object value) { return value is NpcPlanningState && Equals((NpcPlanningState)value); }
        public override int GetHashCode()
        { return unchecked(((Low.GetHashCode() * 397 ^ A.GetHashCode()) * 397 ^ B.GetHashCode()) * 397 ^ C.GetHashCode()); }
    }
    [Serializable] sealed class NpcPlanningExtension
    {
        public NpcPlanningState Requires, Forbids, Adds, Removes;
    }
    [Serializable] sealed class NpcPlanningResult
    {
        public NpcPlanningState State;
        public static NpcPlanningResult Extra(NpcPlanningState state)
        { if (!state.Extended) return null; state.Low = 0; return new NpcPlanningResult { State = state }; }
    }
}
