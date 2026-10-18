using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PolicePursuitRadioScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-pursuit-radio", () => TownScenarioFactory.Arena(5843,
            "........................................", "........................................", "........................................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 5843;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor source = PolicePursuitRumorScenario.Cop(world, "source", 1, false);
            Actor suspect = NpcIntentSupport.Actor(world, "suspect", 2, 1, "timid");
            Actor outsider = NpcIntentSupport.Actor(world, "outsider", 3, 1, "timid");
            Actor patrol = PolicePursuitRumorScenario.Cop(world, "patrol", 25, true);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 18, 1, "sociable");
            ItemRadio gangRadio = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_GANG]);
            suspect.Inventory.AddAll(gangRadio);

            world.Game.DoMakeAggression(suspect, source);
            world.Map.RemoveActor(source); world.Place(source, 35, 2);
            for (int turn = 0; turn < 5 && !relay.Personality.Knowledge.Facts.Any(f => f.Kind == "pursuit_announced"); turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patrol);
            }
            NpcFact rumor = relay.Personality.Knowledge.Facts.Single(f => f.Kind == "pursuit_announced");
            NpcIntent pursuit = NpcIntentSupport.Intent(patrol, ConflictResolutionModule.PolicePursuitId);
            Check.Equal(true, pursuit != null && pursuit.Deadline >= rumor.EventTurn + ConflictResolutionModule.PolicePursuitTurns,
                "police pursue the reported suspect for up to two days");
            Check.Equal(null, NpcIntentSupport.Intent(suspect, "avoid_reported_threat"),
                "the suspect has no pursuit response before listening to the radio");

            int broadcastTurn = 27 * WorldTime.TURNS_PER_HOUR;
            world.Map.LocalTime.TurnCounter = broadcastTurn;
            Session.Get.WorldTime.TurnCounter = broadcastTurn;
            RadioProgram survivor = null;
            for (int slot = 24; slot < 48; slot++)
            {
                survivor = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, slot);
                if (survivor.Facts != null && survivor.Facts.Any(f => f.EventId == rumor.EventId)) break;
            }
            RadioProgram gang = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, 27);
            Check.Equal(true, survivor.Facts != null && survivor.Facts.Any(f => f.EventId == rumor.EventId),
                "Survivor Network can relay the known pursuit");
            Check.Equal(true, gang.Facts != null && gang.Facts.Any(f => f.EventId == rumor.EventId),
                "Gang Frequency can relay the same sourced pursuit");
            Check.Equal(false, suspect.Personality.Knowledge.Facts.Any(f => f.EventId == rumor.EventId),
                "an unpowered radio does not tell the suspect about the pursuit");
            Check.Call(world.Game, "DoUseRadio", new[] { typeof(Actor), typeof(ItemRadio) }, suspect, gangRadio);
            Check.Equal(true, suspect.Personality.Knowledge.Facts.Any(f => f.EventId == rumor.EventId),
                "the suspect hears the named pursuit from their gang radio");
            Check.Equal(true, NpcIntentSupport.Intent(suspect, "avoid_reported_threat") != null,
                "the suspect reacts after a next-day broadcast rather than when the pursuit began");
            Check.Equal(null, NpcIntentSupport.Intent(outsider, "avoid_reported_threat"),
                "a nearby NPC without the tuned radio does not learn the broadcast");

            NpcIntentSupport.Turn(world, suspect);
            Check.Equal(true, NpcIntentSupport.Intent(suspect, "avoid_reported_threat").Finished,
                "the first evasion episode ends when the officer is still far away");
            world.Map.RemoveActor(patrol); world.Place(patrol, 6, 1);
            world.Map.LocalTime.TurnCounter = rumor.EventTurn + ConflictResolutionModule.PolicePursuitTurns - 10;
            Session.Get.WorldTime.TurnCounter = world.Map.LocalTime.TurnCounter;
            suspect.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(suspect), "suspect reacts when the pursuer approaches");
            Check.Equal(true, suspect.Personality.Intents.Any(i => i.DefinitionId == "avoid_reported_threat" && !i.Finished),
                "the suspect can evade again when the officer approaches near the two-day limit");
        });
    }
}
