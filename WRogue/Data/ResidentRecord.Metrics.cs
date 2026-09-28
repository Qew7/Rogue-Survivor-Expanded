using System;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecord
    {
        public string FactionName, GroupName;
        public long ItemsReceived;
        public int InventoryUnits, InventoryStacks, SnapshotTurn, Traits;
        internal void Snapshot(Actor actor)
        {
            FactionName = actor.Faction == null ? "Unknown" : actor.Faction.Name;
            GroupName = actor.Leader != null ? actor.Leader.UnmodifiedName :
                actor.CountFollowers > 0 ? actor.UnmodifiedName : "";
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
