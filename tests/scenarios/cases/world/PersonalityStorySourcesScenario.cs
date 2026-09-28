using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityStorySourcesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/personality-story-sources", () => TownScenarioFactory.Arena(4577,
            "...#...", "...#...", "...#..."), world =>
        {
            Session session = Session.Get;
            session.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            player.IsBotPlayer = true;
            world.Place(player, 2, 1);
            world.SetPlayer(player);
            typeof(RogueGame).GetField("m_PlayerFOV", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Game, new HashSet<Point>());
            Actor absent = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "absent unique", true, false, 0);
            absent.IsDead = true;
            foreach (PropertyInfo property in typeof(UniqueActors).GetProperties())
                property.SetValue(session.UniqueActors, new UniqueActor { TheActor = absent }, null);
            session.UniqueItems.TheSubwayWorkerBadge = new UniqueItem {
                TheItem = new ItemFood(world.Game.GameItems.CANNED_FOOD) };
            District district = world.Map.District;
            Map surface = new Map(4577, "surface", 3, 3);
            district.EntryMap = surface;
            district.AddUniqueMap(world.Map);
            session.UniqueMaps.CHARUndergroundFacility.TheMap = world.Map;
            world.Map.IsSecret = true;
            surface.SetExitAt(new Point(0, 0), new Exit(world.Map, new Point(0, 0)));
            world.Map.SetExitAt(new Point(0, 0), new Exit(surface, new Point(0, 0)));
            Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
            witness.Personality = new PersonalityState();
            witness.Personality.AddTrait(new TraitInstance("skeptic"));
            witness.Personality.AddTrait(new TraitInstance("suspicious"));
            Actor hidden = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "hidden", true, false, 0);
            hidden.Personality = new PersonalityState();
            world.Place(witness, 1, 1);
            world.Place(hidden, 5, 1);
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(true, session.CHARUndergroundFacility_Activated, "real discovery activates CHAR facility");
            Check.Equal(true, surface.GetExitAt(0, 0).IsAnAIExit, "real story discovery opens AI exit");
            Check.Equal("char_discovered", witness.Personality.Memories[0].Id,
                "story discovery creates its specific memory");
            Check.Equal(0, hidden.Personality.Memories.Count, "wall hides the story discovery");
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(1, witness.Personality.Memories.Count, "completed discovery does not repeat");
            Actor prisoner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "prisoner", true, false, 0);
            prisoner.Personality = new PersonalityState();
            world.Place(prisoner, 2, 2);
            session.UniqueActors.PoliceStationPrisoner = new UniqueActor { TheActor = prisoner, IsSpawned = true };
            session.UniqueMaps.PoliceStation_JailsLevel.TheMap = world.Map;
            session.ScriptStage_PoliceStationPrisoner = ScriptStage.STAGE_1;
            Check.Call(world.Game, "CheckSpecialPlayerEventsAfterAction", new[] { typeof(Actor) }, player);
            Check.Equal(true, prisoner.IsDead, "real prisoner script kills the former prisoner");
            Check.Equal(world.Game.GameActors.ZombiePrince, world.Map.GetActorAt(2, 2).Model,
                "real prisoner script creates its monster");
            Check.Equal(ScriptStage.STAGE_2, session.ScriptStage_PoliceStationPrisoner,
                "script advances only after transformation");
            RelationshipRecord personal = witness.Personality.Person(prisoner.PersonalityIdentity);
            Check.Equal("prisoner_transformed", personal.Memories[0].Id,
                "transformation memory remains attributed to the former prisoner");
            Check.Equal(0, hidden.Personality.Memories.Count, "wall hides the transformation");
            foreach (MemoryInstance memory in witness.Personality.Memories)
                world.Map.LocalTime.TurnCounter = Math.Max(world.Map.LocalTime.TurnCounter, memory.ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, witness.Personality.HasTrait("char_whistleblower"), "discovery develops its special trait");
            Check.Equal(true, witness.Personality.HasTrait("betrayal_scar"), "transformation develops its special trait");
            Check.Equal(1, personal.Memories.Count, "resolved story memory stays in the prisoner's history");
        });
    }
}
