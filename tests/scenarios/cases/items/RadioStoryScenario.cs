using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;
static class RadioStoryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/radio-story", () => TownScenarioFactory.Arena(4636,
            "....#.....", "....#.....", "....#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 7, 1);
            player.AudioRangeMod = -20;
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "sociable");
            Actor relay = NpcIntentSupport.Actor(world, "relay", 5, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 3, 1);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            player.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName).Attachment = 30;
            player.Personality.AddTrait(new TraitInstance("timid"));
            int startingSanity = player.Sanity;
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0, storyId: "radio-chapter"));
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", victim, attacker,
                world.Map, victim.Location.Position, 1, storyId: "radio-chapter"));
            NpcFact first = witness.Personality.Knowledge.Facts.First(f => f.Kind == "attack");
            world.Place(witness, 6, 1);
            NpcConversation.ShareRumor(world.Game, witness, relay, first, true);
            Check.Equal(0, player.Personality.Knowledge.Facts.Count, "wall hides original chapter");
            // A legacy receiver in an older save still refers to the handheld item sprite.
            var receiver = new RadioReceiver(GameImages.ITEM_POLICE_RADIO);
            world.Map.PlaceMapObjectAt(receiver, new Point(8, 1));
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            int ap = player.ActionPoints;
            Check.Equal(true, world.Try(new ActionSwitchRadio(player, world.Game, receiver)), "player tunes house radio");
            Check.Equal(0, receiver.Station, "first station is survivors");
            Check.Equal(true, player.ActionPoints < ap, "tuning costs an action");
            Check.Equal(2, player.Personality.Knowledge.Facts.Count(f => f.StoryId == "radio-chapter"),
                "broadcast transmits both linked facts as hearsay");
            Check.Equal(true, player.Personality.HeardJournal.Last().Text.Contains(" After that, "),
                "broadcast is saved as one connected story");
            Check.Equal(startingSanity - 1, player.Sanity,
                "personal attack and aid news affect a timid listener once each");
            RadioProgram localProgram = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 0);
            Session.Get.RadioHostId = attacker.PersonalityIdentity;
            Check.Same(localProgram, Check.Call(world.Game, "GetRadioProgram", 2, 0),
                "changing survivor host does not reset another channel midhour");
            Session.Get.RadioHostId = Guid.Empty;
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY + 4;
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY + 4;
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", victim, attacker,
                world.Map, victim.Location.Position, WorldTime.TURNS_PER_DAY + 3, storyId: "radio-chapter"));
            NpcFact continuation = victim.Personality.Knowledge.Facts.Last(f => f.StoryId == "radio-chapter");
            NpcKnowledgeSystem.Hear(world.Game, witness, victim, continuation);
            NpcConversation.ShareRumor(world.Game, witness, relay, continuation, true);
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", victim, attacker,
                world.Map, victim.Location.Position, WorldTime.TURNS_PER_DAY + 4, storyId: "unrelated"));
            NpcFact unrelated = victim.Personality.Knowledge.Facts.Last(f => f.StoryId == "unrelated");
            NpcKnowledgeSystem.Hear(world.Game, witness, victim, unrelated);
            NpcConversation.ShareRumor(world.Game, witness, relay, unrelated, true);
            for (int slot = 48; slot <= 71; slot++)
            {
                world.Map.LocalTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                    0, world.Map, receiver.Location.Position, null);
                if (player.Personality.Knowledge.Facts.Any(f => f.EventId == continuation.EventId) &&
                    player.Personality.Knowledge.Facts.Any(f => f.EventId == unrelated.EventId)) break;
            }
            Check.Equal(true, player.Personality.Knowledge.Facts.Any(f => f.EventId == continuation.EventId),
                "radio eventually follows a familiar story");
            Check.Equal(true, player.Personality.Knowledge.Facts.Any(f => f.EventId == unrelated.EventId),
                "radio also airs an unrelated story");
            for (int i = 0; i < 4; i++) world.Try(new ActionSwitchRadio(player, world.Game, receiver));
            Check.Equal(false, receiver.IsOn, "cycling all four stations switches off");
            Session.Get.RadioHostId = attacker.PersonalityIdentity;
            player.Personality.Knowledge.Facts.Clear();
            attacker.IsDead = true;
            world.Try(new ActionSwitchRadio(player, world.Game, receiver));
            Check.Equal(0, player.Personality.Knowledge.Facts.Count,
                "survivor station falls silent when its host dies");
            for (int i = 0; i < 4; i++) world.Try(new ActionSwitchRadio(player, world.Game, receiver));
            world.Place(player, 5, 0);
            Check.Equal(false, new ActionSwitchRadio(player, world.Game, receiver).IsLegal(),
                "distant actor cannot tune radio");
            world.Place(player, 7, 1);
            Check.Equal(true, world.Try(new ActionPush(player, world.Game, receiver, Direction.E)),
                "radio can be pushed");
            Check.Equal(new Point(9, 1), receiver.Location.Position, "pushing moves the radio");
            ItemRadio portable = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_SURVIVORS]);
            player.Inventory.AddAll(portable);
            Check.Equal(true, world.Try(new ActionUseItem(player, world.Game, portable)), "portable receiver turns on");
            Check.Equal(true, portable.IsOn, "portable power state changes");
            world.Map.LocalTime.TurnCounter = 30;
            int battery = portable.Batteries;
            Check.Call(world.Game, "AdvanceRadios", world.Map);
            Check.Equal(battery - 1, portable.Batteries, "playing receiver uses its battery");
            Check.Equal(true, world.Try(new ActionUseItem(player, world.Game, portable)), "portable receiver turns off");
            portable.Batteries = 0;
            Check.Equal(false, new ActionUseItem(player, world.Game, portable).IsLegal(),
                "empty portable receiver cannot switch on");
            Session.Get.WorldTime.TurnCounter = 100 * WorldTime.TURNS_PER_HOUR - 1;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                1, world.Map, player.Location.Position, player);
            Check.Equal(99, Session.Get.RadioPrograms[1].Slot, "program stays fixed through the last turn of the hour");
            Session.Get.WorldTime.TurnCounter = 100 * WorldTime.TURNS_PER_HOUR;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                1, world.Map, player.Location.Position, player);
            Check.Equal(100, Session.Get.RadioPrograms[1].Slot, "next game hour starts a new program");
            string path = Path.Combine(Path.GetTempPath(), "radio-story-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session saved = BinarySaveStore.Load<Session>(path);
                saved.World[0, 0].EntryMap.ReconstructAuxiliaryFields();
                RadioReceiver savedReceiver = saved.World[0, 0].EntryMap.MapObjects.OfType<RadioReceiver>().First();
                Check.Equal(GameImages.OBJ_RADIO, savedReceiver.ImageID,
                    "loading an older radio switches its visible tile to the dedicated sprite");
                Check.Equal(GameImages.OBJ_RADIO, savedReceiver.HiddenImageID,
                    "loading an older radio switches its remembered tile too");
                Actor savedPlayer = NpcIntentSupport.Find(saved.World[0, 0].EntryMap, player.PersonalityIdentity);
                Check.Equal(false, savedReceiver.IsOn, "map radio state survives saving");
                Check.Equal(true, savedPlayer.Inventory.Items.OfType<ItemRadio>().Any(), "portable radio survives saving");
                Check.Equal(false, savedPlayer.Personality.FirstRadioHearing(1, 100),
                    "listener remembers the current station program after loading");
                Check.Equal(Session.Get.RadioHostId, saved.RadioHostId, "broadcaster identity survives saving");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
