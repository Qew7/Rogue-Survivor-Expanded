using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RadioArrivalsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-arrivals", () => TownScenarioFactory.Arena(5942,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            District district = world.Map.District;
            ScenarioWorld cellar = new ScenarioWorld(5943, "...", "...", "...");
            district.SewersMap = cellar.Map;
            cellar.Map.SetExitAt(new Point(1, 1), new Exit(world.Map, new Point(1, 1)));
            Actor witness = NpcIntentSupport.Actor(world, "witness", 2, 1);
            Actor psycho = NpcIntentSupport.Actor(world, "wanderer", 5, 1);
            psycho.Faction = world.Game.GameFactions.ThePsychopaths;
            world.Map.RemoveActor(psycho);
            cellar.Map.PlaceActorAt(psycho, new Point(1, 1));
            world.Game.DoLeaveMap(psycho, new Point(1, 1), false);
            NpcFact arrival = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "psychopaths_arrival");
            Check.Equal(true, arrival != null, "entering the district publishes a psychopath sighting");
            Actor broadcaster = NpcIntentSupport.Actor(world, "broadcaster", 3, 1);
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, broadcaster, witness, arrival),
                "sighting can become a retold radio source");
            bool survivorAired = false;
            for (int slot = 24; slot <= 47; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, slot);
                if (program.Facts != null && Array.Exists(program.Facts, f => f.EventId == arrival.EventId))
                { survivorAired = program.Text.Contains("was seen entering the district"); break; }
            }
            Check.Equal(true, survivorAired, "underground survivor station reports the actual arrival");
            Actor player = NpcIntentSupport.Player(world, 6, 2);
            player.Personality.AddTrait(new TraitInstance("timid"));
            int sanity = player.Sanity;
            Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                0, world.Map, player.Location.Position, null);
            Check.Equal(sanity - 1, player.Sanity, "a timid player is unsettled by the psychopath warning");

            world.Map.RemoveActor(broadcaster);
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            Actor biker = NpcIntentSupport.Actor(world, "biker", 4, 1);
            biker.Faction = world.Game.GameFactions.TheBikers;
            Check.Call(world.Game, "NotifyOrderablesAI",
                new[] { typeof(Map), typeof(RaidType), typeof(Point), typeof(Actor) },
                world.Map, RaidType.BIKERS, biker.Location.Position, biker);
            NpcFact gang = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "bikers_raid");
            world.Place(broadcaster, 3, 1);
            Check.Equal(true, gang != null && NpcKnowledgeSystem.Hear(world.Game, broadcaster, witness, gang),
                "real gang arrival reaches a broadcaster as hearsay");
            bool gangAired = false;
            for (int slot = 48; slot <= 71; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, slot);
                if (program.Facts != null && Array.Exists(program.Facts, f => f.EventId == gang.EventId))
                { gangAired = program.Text.Contains("bikers arrived in the district"); break; }
            }
            Check.Equal(true, gangAired, "gang station frames the actual raid as a crew arrival");
            RadioProgram military = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 1, 40);
            Check.Equal(false, military.Facts != null && Array.Exists(military.Facts, f => f.EventId == gang.EventId),
                "gang news stays off military dispatch");
        });
    }
}
