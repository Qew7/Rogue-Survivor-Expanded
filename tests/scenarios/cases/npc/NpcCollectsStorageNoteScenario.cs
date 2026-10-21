using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcCollectsStorageNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/collects-storage-note", () => TownScenarioFactory.Arena(4785,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor reader = NpcIntentSupport.Actor(world, "reader", 1, 1, "curious");
            Actor writer = NpcIntentSupport.Actor(world, "writer", 6, 1);
            NpcIntentSupport.Player(world, 7, 2);
            var drawer = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(drawer, new Point(2, 1));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(writer, NoteTopic.Location, writer.Location, Guid.Empty, null, 0, true);
            drawer.Contents.AddAll(note);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, reader,
                sensor.Sense(world.Game, reader), sensor.FOV);
            Check.Equal(false, reader.Personality.Knowledge.Places.Exists(p =>
                p.Kind == "note:storage" && p.Units > 0),
                "closed drawer does not expose the note inside");
            Check.Equal(true, world.Try(new ActionUseStorageContainer(reader, world.Game, drawer, true)),
                "reader opens the nearby drawer");
            NpcKnowledgeSystem.Perceive(world.Game, reader,
                sensor.Sense(world.Game, reader), sensor.FOV);
            Check.Equal(true, reader.Personality.Knowledge.Places.Exists(p =>
                p.Kind == "note:storage" && p.Units > 0),
                "the open drawer exposes a paper note without revealing its text");
            Check.Equal(false, reader.Personality.Knowledge.HasReadNote(note.Id),
                "seeing the note does not count as reading it");
            for (int i = 0; i < 12 && !reader.Inventory.Contains(note); i++)
            {
                reader.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(reader);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, reader.Inventory.Contains(note),
                "NPC takes the physical note from the open drawer");
            bool readOffered = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, reader))
                if (candidate.Capability.Id == "read_carried_note") readOffered = true;
            Check.Equal(false, readOffered, "reading does not create a lasting goal");
            for (int i = 0; i < 24 && !reader.Personality.Knowledge.HasReadNote(note.Id); i++)
            {
                reader.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(reader);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, reader.Personality.Knowledge.HasReadNote(note.Id),
                "NPC reads the collected note as a separate action");
        });
    }
}
