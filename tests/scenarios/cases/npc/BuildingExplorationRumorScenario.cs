using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class BuildingExplorationRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/building-exploration-rumor", () => TownScenarioFactory.Arena(4931,
            "........", "....#...", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            var grocery = new Zone("Grocery", new Rectangle(2, 1, 2, 1)) { BuildingKind = BuildingKind.Grocery };
            world.Map.AddZone(grocery);
            Actor explorer = NpcIntentSupport.Actor(world, "Vasily", 1, 1);
            Actor companion = NpcIntentSupport.Actor(world, "companion", 1, 2);
            explorer.AddFollower(companion);
            companion.Personality.Opinion(explorer.PersonalityIdentity, explorer.UnmodifiedName);
            Actor sleeper = NpcIntentSupport.Actor(world, "sleeper", 0, 1);
            sleeper.IsSleeping = true;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 2);

            Check.Equal(false, world.Try(new ActionMoveStep(explorer, world.Game, new Point(4, 1))),
                "a blocked move does not explore a building");
            Check.Equal(false, NpcIntentSupport.HasEvent(explorer, "building_explored"),
                "failed movement creates no exploration event");
            Check.Equal(true, world.Try(new ActionMoveStep(explorer, world.Game, new Point(2, 1))),
                "explorer enters the building");
            NpcFact own = explorer.Personality.Knowledge.Facts.Find(f => f.Kind == "building_explored");
            NpcFact seen = companion.Personality.Knowledge.Facts.Find(f => f.Kind == "building_explored");
            Check.Equal(true, own != null && seen != null && own.EventId == seen.EventId,
                "explorer and nearby group member know the same event");
            Check.Equal(NpcKnowledgeSource.Participant, own.Source, "explorer is a participant");
            Check.Equal(NpcKnowledgeSource.Witness, seen.Source, "group member is an eyewitness");
            Check.Equal(explorer.PersonalityIdentity, seen.SubjectId, "fact attributes the real explorer");
            Check.Equal(false, sleeper.Personality.Knowledge.Facts.Exists(f => f.Kind == "building_explored"),
                "sleeping NPC does not witness exploration");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.Kind == "building_explored"),
                "NPC behind a wall does not learn the event remotely");

            Check.Equal(true, world.Try(new ActionMoveStep(explorer, world.Game, new Point(1, 1))),
                "explorer leaves the building");
            Check.Equal(true, world.Try(new ActionMoveStep(explorer, world.Game, new Point(2, 1))),
                "explorer reenters the building");
            Check.Equal(1, explorer.Personality.Knowledge.Facts.FindAll(f => f.Kind == "building_explored").Count,
                "reentry does not generate a duplicate rumor");

            world.Place(companion, 5, 2);
            Check.Equal(true, world.Try(new ActionNpcTell(companion, world.Game, listener, seen)),
                "group member can tell what they witnessed");
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == own.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source, "listener receives hearsay");
            string place = "Vasily explored a building in district " + World.CoordToString(0, 0) + ", at the grocery store";
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.Text.Contains(place)),
                "the spoken rumor names the explorer, district, and building");

            world.Place(explorer, 6, 1);
            Actor fresh = NpcIntentSupport.Actor(world, "fresh listener", 7, 1);
            Check.Equal(true, world.Try(new ActionNpcTell(explorer, world.Game, fresh, own)),
                "the explorer can also tell their own story");
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.CauseId == own.EventId &&
                e.Text.Contains("I was involved when")), "self-report keeps participant wording");

            var disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            var office = new Zone("Office", new Rectangle(6, 0, 1, 1)) { BuildingKind = BuildingKind.Office };
            world.Map.AddZone(office);
            Check.Equal(true, world.Try(new ActionMoveStep(explorer, world.Game, new Point(6, 0))),
                "movement remains legal without personalities");
            Check.Equal(1, explorer.Personality.Knowledge.Facts.FindAll(f => f.Kind == "building_explored").Count,
                "disabled personalities produce no exploration rumor");
        });
    }
}
