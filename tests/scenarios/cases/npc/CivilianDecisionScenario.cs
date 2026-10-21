using djack.RogueSurvivor.Data;

static class CivilianDecisionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/civilian-decision", () => TownScenarioFactory.Create(4201, true), world =>
        {
            Check.Equal(true, world.Map.CountActors > 0, "inhabitants generated");
            Actor civilian = null;
            foreach (Actor actor in world.Map.Actors) { civilian = actor; break; }
            Check.Equal(true, civilian.Controller != null, "civilian has a real AI controller");
            int before = civilian.ActionPoints;
            int equippedBefore = EquippedCount(civilian);
            var action = civilian.Controller.GetAction(world.Game);
            Check.Equal(true, world.Try(action), "civilian chose a legal action");
            Check.Equal(true, civilian.ActionPoints < before || EquippedCount(civilian) > equippedBefore,
                "civilian acted or equipped an item with " + action.GetType().Name);
            Check.Same(civilian, world.Map.GetActorAt(civilian.Location.Position),
                "civilian remains registered on map after its decision");
        });
    }

    static int EquippedCount(Actor actor)
    {
        int count = 0;
        foreach (Item item in actor.Inventory.Items) if (item.IsEquipped) count++;
        return count;
    }
}
