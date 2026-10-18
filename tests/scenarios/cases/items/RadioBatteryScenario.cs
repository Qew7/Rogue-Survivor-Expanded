using System;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;

static class RadioBatteryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/radio-battery", () => TownScenarioFactory.Arena(5944,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            ItemRadio radio = new ItemRadio((ItemTrackerModel)world.Game.GameItems[GameItems.IDs.RADIO_MILITARY]);
            player.Inventory.AddAll(radio);
            radio.EquippedPart = DollPart.LEFT_HAND;
            radio.IsOn = true;
            radio.Batteries = 2;

            Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
            world.Map.LocalTime.TurnCounter = 1;
            Check.Call(world.Game, "NextMapTurn", new[] { typeof(Map), flags },
                world.Map, Enum.Parse(flags, "NOT_SIMULATING"));
            Check.Equal(2, radio.Batteries, "equipping a receiver does not drain tracker batteries each turn");

            world.Map.LocalTime.TurnCounter = 30;
            Check.Call(world.Game, "AdvanceRadios", world.Map);
            Check.Equal(1, radio.Batteries, "playing receiver spends one charge on its interval");
            Check.Equal(true, radio.IsOn, "receiver stays on while charged");
            world.Map.LocalTime.TurnCounter = 60;
            Check.Call(world.Game, "AdvanceRadios", world.Map);
            Check.Equal(0, radio.Batteries, "final interval spends the last charge");
            Check.Equal(false, radio.IsOn, "receiver turns off as soon as charge reaches zero");
            world.Map.LocalTime.TurnCounter = 90;
            Check.Call(world.Game, "AdvanceRadios", world.Map);
            Check.Equal(0, radio.Batteries, "empty receiver cannot broadcast or spend more charge");
        });
    }
}
