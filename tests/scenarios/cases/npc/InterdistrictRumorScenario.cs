using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class InterdistrictRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/interdistrict-rumor", () => TownScenarioFactory.Arena(4976,
            "...#....", "...#....", "...#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 1, 1);
            Actor carrier = NpcIntentSupport.Actor(world, "carrier", 6, 1, "sociable");
            District neighboring = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
            Map destination = new Map(4977, "neighboring", 8, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 8; x++)
                destination.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            neighboring.EntryMap = destination;
            World city = new World(2);
            city[0, 0] = world.Map.District;
            city[1, 0] = neighboring;
            Session.Get.World = city;
            Point border = new Point(8, 1);
            world.Map.SetExitAt(border, new Exit(destination, new Point(0, 1)) { IsAnAIExit = true });
            destination.SetExitAt(new Point(-1, 1), new Exit(world.Map, new Point(7, 1)) { IsAnAIExit = true });
            Actor listener = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "listener", true, false, 0);
            listener.Controller = new CivilianAI(); listener.Personality = new PersonalityState();
            destination.PlaceActorAt(listener, new Point(1, 1));

            int day = 0;
            while (true)
            {
                uint routeSeed = (uint)(carrier.PersonalityIdentity.GetHashCode() ^ (day * 1601));
                if (routeSeed % 5u == 0 && (routeSeed / 5u) % 4u == 1u) break;
                day++;
            }
            int turn = day * WorldTime.TURNS_PER_DAY;
            world.Map.LocalTime.TurnCounter = turn;
            destination.LocalTime.TurnCounter = turn;
            Session.Get.WorldTime.TurnCounter = turn;
            PersonalitySystem.Report(world.Game, new SignificantEvent("building_explored", carrier, null,
                world.Map, carrier.Location.Position, turn, storyId: "across-districts"));
            NpcFact report = carrier.Personality.Knowledge.Facts.Find(f => f.Kind == "building_explored");
            Check.Equal(true, report != null, "carrier knows an actual event before crossing");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == report.EventId),
                "neighboring NPC has not heard the event");
            destination.LocalTime.TurnCounter = turn - 1;
            Check.Equal(false, world.Game.Rules.CanActorUseExit(carrier, border),
                "NPC cannot enter a district in the past");
            destination.LocalTime.TurnCounter = turn;
            for (int step = 0; step < 8 && carrier.Location.Map != destination; step++)
            {
                carrier.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(carrier), "civilian performs a legal crossing step");
            }
            Check.Same(destination, carrier.Location.Map, "civilian reaches neighboring district through the exit");
            Check.Equal(false, world.Map.Actors.Contains(carrier), "carrier is absent from the old map");
            Check.Equal(0, carrier.ActionPoints, "crossing cannot grant another action in the destination turn");
            carrier.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(false, carrier.Controller.GetAction(world.Game) is ActionUseExit,
                "civilian at the day's destination does not turn back immediately");

            carrier.ActionPoints = Rules.BASE_ACTION_COST;
            ActionNpcTell tell = new ActionNpcTell(carrier, world.Game, listener, report);
            Check.Equal(true, tell.IsLegal(), "arrived carrier can address a local listener");
            tell.Perform();
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.EventId == report.EventId);
            Check.Equal(true, heard != null && heard.Source == NpcKnowledgeSource.Told,
                "original event becomes a spoken rumor in the next district");
            Check.Equal(report.StoryId, heard.StoryId, "the story link survives the district boundary");

            string path = Path.Combine(Path.GetTempPath(), "interdistrict-rumor-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Actor saved = NpcIntentSupport.Find(loaded.World[1, 0].EntryMap, carrier.PersonalityIdentity);
                Check.Equal(true, saved.Personality.Knowledge.Facts.Exists(f => f.EventId == report.EventId),
                    "carrier and its rumor remain in the destination after loading");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
