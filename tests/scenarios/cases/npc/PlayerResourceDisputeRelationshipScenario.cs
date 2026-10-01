using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlayerResourceDisputeRelationshipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-resource-dispute-relationship", () => TownScenarioFactory.Arena(4826,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor claimant = NpcIntentSupport.Actor(world, "claimant", 2, 1);
            SignificantEvent dispute = new SignificantEvent("resource_contested", claimant, player,
                world.Map, claimant.Location.Position, world.Map.LocalTime.TurnCounter)
            { Resource = "food", ResourcePlace = player.Location };
            PersonalitySystem.Report(world.Game, dispute);
            Check.Equal(true, player.Personality.Person(claimant.PersonalityIdentity) != null,
                "direct food dispute creates a personal relationship");
            IList<string> lines = (IList<string>)Check.Call(world.Game, "RelationshipLines",
                new[] { typeof(Actor) }, player);
            Check.Equal(true, String.Join(" ", new List<string>(lines).ToArray()).Contains("claimant: wary"),
                "food claimant appears in Shift+I after dispute");
        });
    }
}
