using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;

static class RadioProgramScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-program", () => TownScenarioFactory.Arena(4636,
            "........", "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            World city = new World(2);
            city[0, 0] = world.Map.District;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                if (x == 0 && y == 0) continue;
                Map map = new Map(4636 + x * 2 + y, "other district", 8, 4);
                for (int row = 0; row < map.Height; row++) for (int col = 0; col < map.Width; col++)
                    map.SetTileModelAt(col, row, world.Game.GameTiles.FLOOR_ASPHALT);
                District district = new District(new Point(x, y), DistrictKind.GENERAL);
                district.EntryMap = map;
                city[x, y] = district;
            }
            Session.Get.World = city;
            Map remote = city[1, 0].EntryMap;
            Actor listener = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "remote listener", true, false, 0);
            listener.Personality = new PersonalityState();
            remote.PlaceActorAt(listener, new Point(1, 1));
            Actor source = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "source", true, false, 0);
            source.Personality = new PersonalityState();
            remote.PlaceActorAt(source, new Point(6, 1));
            source.Personality.Knowledge.Facts.Add(new NpcFact { EventId = 2, Kind = "shared_food",
                StoryId = "familiar", EventTurn = 0, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 60, Hops = 1, SubjectName = "Ada" });
            source.Personality.Knowledge.Facts.Add(new NpcFact { EventId = 3, Kind = "shared_food",
                StoryId = "unrelated", EventTurn = 0, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 60, Hops = 1, SubjectName = "Ben" });
            player.Personality.Knowledge.Facts.Add(new NpcFact { EventId = 1, Kind = "shared_food",
                StoryId = "familiar", EventTurn = 0, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 60, Hops = 1, SubjectName = "Ada" });
            RadioReceiver homeRadio = new RadioReceiver(GameImages.ITEM_POLICE_RADIO);
            world.Map.PlaceMapObjectAt(homeRadio, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionSwitchRadio(player, world.Game, homeRadio)),
                "home receiver starts the station's shared program");
            RadioProgram program = Session.Get.RadioPrograms[0];
            Check.Equal(true, program.Facts != null && program.Facts.Length > 0, "news segment uses an actual NPC fact");
            int heard = player.Personality.HeardJournal.Count;
            RadioReceiver remoteRadio = new RadioReceiver(GameImages.ITEM_POLICE_RADIO);
            remote.PlaceMapObjectAt(remoteRadio, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionSwitchRadio(listener, world.Game, remoteRadio)),
                "other district receives the station");
            Check.Equal(true, Object.ReferenceEquals(program, Session.Get.RadioPrograms[0]),
                "both districts use the same cached program");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.EventId == program.EventId),
                "remote listener hears the same headline");
            ItemRadio portable = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_SURVIVORS]);
            listener.Inventory.AddAll(portable);
            Check.Equal(true, world.Try(new ActionUseItem(listener, world.Game, portable)),
                "portable receiver joins the same broadcast");
            Check.Equal(true, Object.ReferenceEquals(program, Session.Get.RadioPrograms[0]),
                "portable receiver cannot regenerate a different headline");
            Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                0, world.Map, homeRadio.Location.Position, null);
            Check.Equal(heard, player.Personality.HeardJournal.Count,
                "tuning twice within an hour does not repeat the journal entry");
            string path = Path.Combine(Path.GetTempPath(), "radio-program-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session saved = BinarySaveStore.Load<Session>(path);
                Check.Equal(program.Text, saved.RadioPrograms[0].Text,
                    "current station program survives saving");
                Check.Equal(program.EventId, saved.RadioPrograms[0].EventId,
                    "saved program retains its real headline");
                Check.Equal(true, saved.RadioPrograms[0].Source != null,
                    "saved program retains the NPC source for hearsay");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            // Keep the player's prior episode fixed while sampling later shared slots.
            player.Personality.Knowledge.Facts.RemoveAll(f => f.EventId != 1);
            int continuation = 0, unfamiliar = 0, filler = 0;
            for (int slot = 1; slot <= 30; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram next = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, slot);
                if (next.EventId == 2) continuation++;
                if (next.EventId == 3) unfamiliar++;
                if (next.Facts == null) filler++;
            }
            Check.Equal(true, continuation > unfamiliar && unfamiliar > 0,
                "familiar continuation is favored without excluding unknown news");
            Check.Equal(true, filler > 0, "station has breaks between reports");
            for (int station = 0; station < 4; station++)
            {
                bool hasLore = false, hasBark = false, hasNews = false;
                for (int slot = 31; slot < 37; slot++)
                {
                    Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                    RadioProgram next = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", station, slot);
                    if (next.Text.Contains("freighter")) hasLore = true;
                    if (next.Facts == null && !next.Text.Contains("freighter")) hasBark = true;
                    if (next.Facts != null) hasNews = true;
                }
                Check.Equal(true, hasLore && hasBark, "each station alternates themed lore and incidental material");
                if (station == 2) Check.Equal(true, hasNews, "Local Calls still airs shared-food reports");
            }
            var barks = new HashSet<string>();
            var cities = new HashSet<string>();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                barks.Add((string)Check.Call(world.Game, "RadioBark", 0, 3 + cycle * 6));
                cities.Add((string)Check.Call(world.Game, "RadioLore", 0, 2 + cycle * 6));
            }
            Check.Equal(4, barks.Count, "hourly breaks rotate through every station line");
            Check.Equal(4, cities.Count, "distant-city reports rotate between cities");
            string origin = (string)Check.Call(world.Game, "RadioLore", 0, 0);
            Session.Get.Seed = 4637;
            string anotherOrigin = (string)Check.Call(world.Game, "RadioLore", 0, 0);
            Check.Equal(false, origin == anotherOrigin,
                "different city seeds give different uncertain origin rumors");
            Session.Get.WorldTime.TurnCounter = 52 * WorldTime.TURNS_PER_HOUR;
            RadioProgram expired = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 52);
            Check.Equal(true, expired.Facts == null && expired.Text != null,
                "expired reports leave an incidental broadcast, not a stale story");
            source.Personality.Knowledge.Facts.Clear();
            source.Personality.Knowledge.Facts.Add(new NpcFact { EventId = 4, Kind = "shared_food",
                StoryId = "weak", EventTurn = 52 * WorldTime.TURNS_PER_HOUR, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 39, Hops = 1, SubjectName = "Cy" });
            Session.Get.WorldTime.TurnCounter = 53 * WorldTime.TURNS_PER_HOUR;
            RadioProgram weak = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 53);
            Check.Equal(null, weak.Facts, "unreliable hearsay cannot headline the program");
        });
    }
}
