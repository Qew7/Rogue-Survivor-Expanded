using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class DoorBarricadeActionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("doors/build-barricade", () => TownScenarioFactory.Arena(4311,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(1, 1));
            world.SetPlayer(actor);
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            Check.Equal(false, world.Game.Rules.CanActorBarricadeDoor(actor, door),
                "barricading requires materials");
            ItemBarricadeMaterial wood = new ItemBarricadeMaterial(world.Game.GameItems.WOODENPLANK);
            actor.Inventory.AddAll(wood);
            Check.Equal(true, world.Try(new ActionBarricadeDoor(actor, world.Game, door)),
                "barricading action legal with material");
            Check.Equal(true, door.BarricadePoints > 0, "door gains barricade points");
            Check.Equal(true, door.IsBarricaded, "door is marked barricaded");
        });
    }
}
