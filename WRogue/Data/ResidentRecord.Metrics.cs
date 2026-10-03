using System;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Data
{
    // Diagnostic state only. Never keep Actor/Map references in the resident archive.
    [Serializable]
    sealed class ResidentSurvivalSnapshot
    {
        public int LocalTurn, WorldTurn;
        public int HitPoints, MaxHitPoints, Stamina, MaxStamina, Food, MaxFood, Sleep, MaxSleep, Sanity, MaxSanity;
        public int Speed, Threat, Resolve, EnemyDistance;
        public bool Hungry, Starving, Sleepy, Exhausted, Tired, Disturbed, Insane, Sleeping, Running, Mortal;
        public string Activity, Reason, OtherName, OtherModel;
        public Guid OtherId;

        public ResidentSurvivalSnapshot(Actor actor, Rules rules, int worldTurn)
        {
            LocalTurn = actor.Location.Map.LocalTime.TurnCounter;
            WorldTurn = worldTurn;
            HitPoints = actor.HitPoints; MaxHitPoints = rules.ActorMaxHPs(actor);
            Stamina = actor.StaminaPoints; MaxStamina = rules.ActorMaxSTA(actor);
            Food = actor.FoodPoints; MaxFood = rules.ActorMaxFood(actor);
            Sleep = actor.SleepPoints; MaxSleep = rules.ActorMaxSleep(actor);
            Sanity = actor.Sanity; MaxSanity = rules.ActorMaxSanity(actor);
            Speed = rules.ActorSpeed(actor);
            Hungry = rules.IsActorHungry(actor); Starving = rules.IsActorStarving(actor);
            Sleepy = rules.IsActorSleepy(actor); Exhausted = rules.IsActorExhausted(actor);
            Tired = rules.IsActorTired(actor);
            Disturbed = rules.IsActorDisturbed(actor); Insane = rules.IsActorInsane(actor);
            Sleeping = actor.IsSleeping; Running = actor.IsRunning;
            Activity = actor.Activity.ToString();
            EnemyDistance = -1;
        }

        public void SetOther(Actor other)
        {
            if (other == null) return;
            OtherId = other.PersonalityIdentity;
            OtherName = other.UnmodifiedName;
            OtherModel = other.Model.Name;
        }
    }

    sealed partial class ResidentRecord
    {
        [System.Runtime.Serialization.OptionalField] public ResidentSurvivalSnapshot ThreatSnapshot;
        [System.Runtime.Serialization.OptionalField] public ResidentSurvivalSnapshot DeathSnapshot;
        public string FactionName, GroupName;
        public Guid GroupIdentity;
        public long ItemsReceived;
        public int InventoryUnits, InventoryStacks, SnapshotTurn, Traits;
        internal void Snapshot(Actor actor)
        {
            FactionName = actor.Faction == null ? "Unknown" : actor.Faction.Name;
            GroupName = actor.Leader != null ? actor.Leader.UnmodifiedName :
                actor.CountFollowers > 0 ? actor.UnmodifiedName : "";
            GroupIdentity = actor.SocialGroup == null ? Guid.Empty : actor.SocialGroup.Identity;
            SnapshotTurn = actor.Location.Map == null ? actor.SpawnTime : actor.Location.Map.LocalTime.TurnCounter;
            InventoryUnits = InventoryStacks = 0;
            if (actor.Inventory != null)
            {
                ItemsReceived = actor.Inventory.TotalReceived;
                InventoryStacks = actor.Inventory.CountItems;
                foreach (Item item in actor.Inventory.Items) InventoryUnits += item.Quantity;
            }
            Traits = actor.Personality == null ? 0 : actor.Personality.Traits.Count;
        }
    }
    sealed partial class ResidentRecords
    {
        public void Refresh(Session session)
        {
            m_DistrictKinds = DistrictKindsFrom(session.World);
            if (session.World == null) return;
            for (int x = 0; x < session.World.Size; x++)
                for (int y = 0; y < session.World.Size; y++)
                {
                    District district = session.World[x, y]; if (district == null) continue;
                    foreach (Map map in district.Maps)
                        foreach (Actor actor in map.Actors)
                        {
                            ResidentRecord record = Register(actor);
                            if (record != null && !actor.IsDead) record.SnapshotTurn = session.WorldTime.TurnCounter;
                        }
                }
        }
    }
}
