using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CachePoliceReportScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-police-report", () => TownScenarioFactory.Arena(4879,
            "....#...", "....#...", "....#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 0, "lawful");
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1);
            witness.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            cop.Faction = world.Game.GameFactions.ThePolice;
            NpcIntentSupport.Player(world, 7, 2);
            var cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position));
            NpcFact theft = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            Check.Equal(true, theft != null && theft.NamesSubject,
                "witness has a named, first hand account");
            Check.Equal(false, cop.Personality.Knowledge.Facts.Exists(f => f.EventId == theft.EventId),
                "police officer across a wall has no remote knowledge");
            world.Map.PlaceActorAt(thief, new Point(7, 0));
            world.Map.PlaceActorAt(cop, new Point(1, 2));
            for (int turn = 0; turn < 15 && !cop.Personality.Knowledge.Facts.Exists(f =>
                f.EventId == theft.EventId); turn++)
            {
                witness.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(witness);
                world.Map.LocalTime.TurnCounter++;
            }
            NpcFact report = cop.Personality.Knowledge.Facts.Find(f => f.EventId == theft.EventId);
            Check.Equal(true, report != null, "witness chooses a real conversation with a known officer");
            Check.Equal(NpcKnowledgeSource.Told, report.Source,
                "officer receives a report rather than eyewitness knowledge");
        });
    }
}
