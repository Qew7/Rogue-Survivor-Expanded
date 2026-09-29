using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ThreatenedHomeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/threatened-home", () => TownScenarioFactory.Arena(4673, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "homebody", "loyal");
            world.Map.GetTileAt(1, 1).IsInside = true;
            world.Map.GetTileAt(2, 1).IsInside = true;
            var claim = new XpdBase(owner, new[] { new Point(1, 1), new Point(2, 1) }); world.Map.AddXpdBase(claim);
            NpcSocialSystem.RememberHome(owner);
            Check.Equal(1, owner.Personality.Attachments.Count, "own visited home establishes a lasting place attachment");
            world.Map.RemoveActor(owner); world.Place(owner, 4, 1);
            Actor threat = NpcIntentSupport.Actor(world, "threat", 0, 1);
            owner.Personality.Knowledge.See(threat, 0);
            NpcKnownPerson known = owner.Personality.Knowledge.Person(threat.PersonalityIdentity); known.Danger = known.ThreatConfidence = 100;
            NpcIntentSupport.Turn(world, owner); NpcIntent goal = NpcIntentSupport.Intent(owner, "seek_group_shelter");
            Check.Equal(NpcGoalValue.ProtectHome, goal.Generated.Value, "known nearby risk makes the remembered place a subject of concern");
            for (int turn = 1; turn <= 5 && !goal.Finished; turn++) { world.Map.LocalTime.TurnCounter = turn; NpcIntentSupport.Turn(world, owner); }
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "actor really reaches the attached home");
            Check.Equal(true, owner.Location.Map.GetTileAt(owner.Location.Position).IsInside, "predicted shelter is verified against the real tile");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "home_reached"), "chronicle distinguishes a personal home from an assigned group shelter");
        });
    }
}
