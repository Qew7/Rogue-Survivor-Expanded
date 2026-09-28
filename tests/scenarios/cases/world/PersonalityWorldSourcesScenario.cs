using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityWorldSourcesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/personality-world-sources", () => TownScenarioFactory.Arena(4573,
            ".........", ".........", ".........", ".........", ".........",
            ".........", ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            typeof(Session).GetField("m_Event_Raids", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(Session.Get, new int[(int)RaidType._COUNT, 1, 1]);
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            world.Place(player, 4, 8);
            world.SetPlayer(player);
            Map elsewhere = new Map(4573, "elsewhere", 2, 2);
            world.Map.RemoveActor(player);
            elsewhere.PlaceActorAt(player, new Point(0, 0));
            Actor witness = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "witness", true, false, 0);
            witness.Personality = new PersonalityState();
            world.Place(witness, 4, 4);
            Check.Equal(false, Check.Call(world.Game, "CheckForEvent_BikersRaid",
                new[] { typeof(Map) }, world.Map), "biker raid is ineligible before its starting day");
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_DAY / 2;
            string[] handlers = { "FireEvent_NationalGuard", "FireEvent_BikersRaid",
                "FireEvent_GangstasRaid", "FireEvent_BlackOpsRaid", "FireEvent_BandOfSurvivors",
                "FireEvent_ArmySupplies", "FireEvent_ZombieInvasion", "FireEvent_SewersInvasion" };
            foreach (string handler in handlers)
            {
                witness.Personality = new PersonalityState();
                foreach (Actor actor in new System.Collections.Generic.List<Actor>(world.Map.Actors))
                    if (actor != witness) world.Map.RemoveActor(actor);
                Check.Call(world.Game, handler, new[] { typeof(Map) }, world.Map);
                Check.Equal(true, witness.Personality.Memories.Count > 0, handler + ": real world effect reports memory");
                Check.Equal(false, witness.Personality.Memories[0].Id == "raid",
                    "world event preserves its specific source rather than generic raid");
                if (handler != "FireEvent_ArmySupplies")
                    Check.Equal(true, world.Map.CountActors > 1, "world handler actually spawns actors");
                else Check.Equal("army_supplies", witness.Personality.Memories[0].Id,
                    "relief supplies generate a peaceful memory");
            }
            witness.Personality = new PersonalityState();
            foreach (Actor actor in new System.Collections.Generic.List<Actor>(world.Map.Actors))
                if (actor != witness) world.Map.RemoveActor(actor);
            foreach (System.Reflection.PropertyInfo property in typeof(UniqueActors).GetProperties())
                property.SetValue(Session.Get.UniqueActors, new UniqueActor(), null);
            GameOptions refugeesOptions = RogueGame.Options;
            refugeesOptions.MaxCivilians = 10;
            typeof(RogueGame).GetField("s_Options", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic).SetValue(null, refugeesOptions);
            Check.Call(world.Game, "FireEvent_RefugeesWave", new[] { typeof(District) }, world.Map.District);
            Check.Equal(true, world.Map.CountActors > 1, "refugees actually arrive on the surface");
            Check.Equal("refugees_arrival", witness.Personality.Memories[0].Id,
                "real refugee wave creates its own memory");
            witness.Personality = new PersonalityState();
            foreach (Actor actor in new System.Collections.Generic.List<Actor>(world.Map.Actors))
                if (actor != witness) world.Map.RemoveActor(actor);
            world.Place(new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "existing zombie", true, false, 0), 2, 2);
            world.Place(new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "another existing zombie", true, false, 0), 3, 2);
            GameOptions options = RogueGame.Options;
            options.MaxUndeads = 10;
            options.DayZeroUndeadsPercent = 10;
            typeof(RogueGame).GetField("s_Options", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic).SetValue(null, options);
            Check.Call(world.Game, "FireEvent_ZombieInvasion", new[] { typeof(Map) }, world.Map);
            Check.Equal(0, witness.Personality.Memories.Count, "failed or unnecessary spawn reports no invasion memory");
        });
    }
}
