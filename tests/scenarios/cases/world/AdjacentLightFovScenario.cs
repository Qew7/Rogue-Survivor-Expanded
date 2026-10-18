using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class AdjacentLightFovScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/adjacent-light-fov", () => TownScenarioFactory.Arena(8102,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            world.Map.Lighting = Lighting.DARKNESS;
            Actor observer = SkillScenario.Actor(world);
            Actor carrier = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(carrier, new Point(2, 2));
            int unlit = world.Game.Rules.ActorFOV(observer, world.Map.LocalTime, world.Game.Session.World.Weather);
            ItemLight light = new ItemLight(world.Game.GameItems.FLASHLIGHT);
            Check.Equal(true, carrier.Inventory.AddAll(light), "light fits carrier inventory");
            Check.Equal(true, world.Try(new ActionEquipItem(carrier, world.Game, light)), "carrier equips light");
            Check.Equal(unlit, world.Game.Rules.ActorFOV(observer, world.Map.LocalTime, world.Game.Session.World.Weather),
                "diagonal light two tiles away does not help");
            world.Map.PlaceActorAt(carrier, new Point(1, 1));
            Check.Equal(unlit + 1, world.Game.Rules.ActorFOV(observer, world.Map.LocalTime, world.Game.Session.World.Weather),
                "diagonal neighbor helps even at map corner");
            light.Batteries = 0;
            Check.Equal(unlit, world.Game.Rules.ActorFOV(observer, world.Map.LocalTime, world.Game.Session.World.Weather),
                "empty neighbor light stops helping");
        });
    }
}
