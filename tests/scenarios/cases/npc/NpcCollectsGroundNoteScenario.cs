using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcCollectsGroundNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/collects-ground-note", () => TownScenarioFactory.Arena(4786,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor reader = NpcIntentSupport.Actor(world, "reader", 1, 1, "curious");
            Actor writer = NpcIntentSupport.Actor(world, "writer", 6, 1);
            NpcIntentSupport.Player(world, 7, 2);
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(writer, NoteTopic.Location, writer.Location, Guid.Empty, null, 0, false);
            Point at = new Point(2, 1);
            world.Map.DropItemAt(note, at);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS | LOSSensor.SensingFilter.ITEMS);
            NpcKnowledgeSystem.Perceive(world.Game, reader,
                sensor.Sense(world.Game, reader), sensor.FOV);
            Check.Equal(true, reader.Personality.Knowledge.Places.Exists(p =>
                p.Kind == "note:ground" && p.Units > 0), "NPC sees a paper note on the ground");
            Check.Equal(false, reader.Personality.Knowledge.HasReadNote(note.Id),
                "visible paper does not disclose its contents");
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, reader))
                Check.Equal(false, candidate.Capability.Id == "collect_visible_note",
                    "visible paper does not create a lasting pickup goal");
            for (int i = 0; i < 12 && !reader.Inventory.Contains(note); i++)
            {
                reader.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(reader);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, reader.Inventory.Contains(note), "NPC physically picks up the note");
            for (int i = 0; i < 8 && !reader.Personality.Knowledge.HasReadNote(note.Id); i++)
            {
                reader.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(reader);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, reader.Personality.Knowledge.HasReadNote(note.Id),
                "NPC reads the picked-up note later");
            Check.Equal(true, world.Try(new ActionDropItem(reader, world.Game, note)),
                "reader can leave the read note on the ground");
            NpcKnowledgeSystem.Perceive(world.Game, reader,
                sensor.Sense(world.Game, reader), sensor.FOV);
            Check.Equal(false, reader.Personality.Knowledge.Places.Exists(p =>
                p.Kind == "note:ground" && p.Units > 0),
                "the same read note does not create another pickup goal");
        });
    }
}
