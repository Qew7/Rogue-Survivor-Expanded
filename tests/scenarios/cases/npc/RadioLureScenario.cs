using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI;

static class RadioLureScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/radio-lure", () => TownScenarioFactory.Arena(4639,
            "....#......", "....#......", "....#......"), world =>
        {
            NpcIntentSupport.Player(world, 0, 1);
            Actor operatorNpc = NpcIntentSupport.Actor(world, "operator", 8, 0);
            RadioReceiver radio = new RadioReceiver("radio");
            world.Map.PlaceMapObjectAt(radio, new Point(8, 1));
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            zombie.Controller = new ZombieAI();
            world.Place(zombie, 5, 1);
            Check.Equal(true, world.Try(new ActionSwitchRadio(operatorNpc, world.Game, radio)),
                "NPC switches on nearby radio");
            world.Place(operatorNpc, 1, 0);
            int before = world.Game.Rules.GridDistance(zombie.Location.Position, radio.Location.Position);
            Check.Equal(true, world.NpcTurn(zombie), "undead acts on radio signal");
            Check.Equal(true, world.Game.Rules.GridDistance(zombie.Location.Position, radio.Location.Position) < before,
                "undead approaches playing radio");
            world.Map.LocalTime.TurnCounter = 30;
            Check.Call(world.Game, "AdvanceRadios", world.Map);
            Check.Equal(60, world.Map.RadioNoiseUntil,
                "an unattended radio still renews its zombie lure");
            world.Place(operatorNpc, 8, 0);
            for (int i = 0; i < 4; i++) world.Try(new ActionSwitchRadio(operatorNpc, world.Game, radio));
            Check.Equal(false, radio.IsOn, "radio can be switched off");
            Check.Equal(world.Map.LocalTime.TurnCounter, world.Map.RadioNoiseUntil,
                "switching off ends the lure");
        });
    }
}
