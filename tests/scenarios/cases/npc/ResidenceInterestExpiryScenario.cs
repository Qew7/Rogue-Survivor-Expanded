using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResidenceInterestExpiryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/residence-interest-expiry", () => TownScenarioFactory.Arena(4817,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor resident = NpcIntentSupport.Actor(world, "resident", 1, 1, "homebody");
            world.Map.GetTileAt(1, 1).IsInside = true;
            world.Map.GetTileAt(4, 1).IsInside = true;
            NpcGoalGenerator.Refresh(world.Game, resident);
            NpcInterest first = resident.Personality.Interest("residence", resident.PersonalityIdentity);
            Check.Equal(new Point(1, 1), first.Place.Position, "an indoor place becomes a chosen residence");

            world.Map.RemoveActor(resident); world.Place(resident, 3, 1);
            world.Map.LocalTime.TurnCounter = first.ExpiresTurn;
            NpcGoalGenerator.Refresh(world.Game, resident);
            Check.Equal(new Point(1, 1), first.Place.Position, "an expired residence does not renew outdoors");

            world.Map.RemoveActor(resident); world.Place(resident, 4, 1);
            NpcGoalGenerator.Refresh(world.Game, resident);
            NpcInterest renewed = resident.Personality.Interest("residence", resident.PersonalityIdentity);
            Check.Equal(new Point(4, 1), renewed.Place.Position, "an expired residence can be chosen again indoors");
            Check.Equal(world.Map.LocalTime.TurnCounter, renewed.CreatedTurn, "renewal begins a new interest period");
            Check.Equal(true, resident.Personality.Interests.Count <= 16, "renewal reuses the existing saved slot");
        });
    }
}
