using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    [Flags]
    enum NpcPlanFact : ulong
    {
        None = 0, Food = 1, SpareFood = 2, Delivered = 4, Reported = 8,
        Contact = 16, Warned = 32, Sheltered = 64, Safe = 128,
        Requested = 256, Left = 512, LocationKnown = 1024, Medicine = 4096, Healthy = 8192, Helped = 16384, ValuedItem = 32768
    }
    enum NpcPlanAction { Travel, PickupFood, AskFood, GiveFood, ReportDelivery, AskLocation,
        Reunite, Warn, Retreat, ConfirmSafety, EnterShelter, LeaveGroup, BarterFood, PickupMedicine, UseMedicine,
        AskMedicine, GiveMedicine, TreatPerson, BarterMedicine, PickupValuedItem, DemandRestitution }
    [Serializable]
    sealed class NpcPlanStep
    {
        public NpcPlanAction Action;
        public Guid Target;
        public Location Place;
        public ulong Requires, Forbids, Adds, Removes;
        public int Cost;
        public bool Applies(ulong state) { return (state & Requires) == Requires && (state & Forbids) == 0; }
        public ulong Predict(ulong state) { return (state & ~Removes) | Adds; }
    }
    [Serializable]
    sealed class NpcPlanFailure
    {
        public NpcPlanAction Action;
        public Guid Target;
        public Location Place;
        public int Until;
    }
    [Serializable]
    sealed class NpcPlan
    {
        public readonly List<NpcPlanStep> Steps = new List<NpcPlanStep>();
        public readonly List<NpcPlanFailure> Failures = new List<NpcPlanFailure>();
        public ulong Desired;
        public int Cursor, Revision, Traits, NextPlanningTurn, Expanded;
        public long LastEventId;
        public NpcPlanStep Current { get { return Cursor < Steps.Count ? Steps[Cursor] : null; } }
        public void Invalidate() { Steps.Clear(); Cursor = 0; }
        public bool Blocked(NpcPlanStep step, int turn)
        { return Failures.Exists(f => f.Until > turn && f.Action == step.Action && f.Target == step.Target && f.Place == step.Place); }
        public void Reject(NpcPlanStep step, int turn)
        {
            Failures.RemoveAll(f => f.Until <= turn);
            if (Failures.Count >= 8) Failures.RemoveAt(0);
            Failures.Add(new NpcPlanFailure { Action = step.Action, Target = step.Target, Place = step.Place, Until = turn + 30 });
            Invalidate(); NextPlanningTurn = turn;
        }
        public void Release() { Invalidate(); Failures.Clear(); }
    }
}
