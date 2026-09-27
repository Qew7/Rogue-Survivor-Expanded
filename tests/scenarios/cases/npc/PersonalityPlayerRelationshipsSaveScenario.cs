using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityPlayerRelationshipsSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-player-relationships-save", () => TownScenarioFactory.Arena(4556,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", true, false, 0);
            player.Controller = new PlayerController();
            Actor attacker = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "attacker", true, false, 0);
            world.Place(player, 1, 1);
            world.Place(attacker, 2, 1);
            world.SetPlayer(player);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", player, attacker,
                world.Map, player.Location.Position, world.Map.LocalTime.TurnCounter));
            Guid attackerId = attacker.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "player-relations-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor savedPlayer = map.GetActorAt(1, 1);
                Check.Equal(-35, savedPlayer.Personality.Person(attackerId).Feeling,
                    "player's personal impression survives save and load");
                Session.Restore(loaded);
                map.LocalTime.TurnCounter = savedPlayer.Personality.Memories[0].ResolveTurn;
                PersonalitySystem.ResolveDue(world.Game, map);
                BinarySaveStore.Save(path, Session.Get);
                Session reloaded = BinarySaveStore.Load<Session>(path);
                Map finalMap = reloaded.World[0, 0].EntryMap;
                finalMap.ReconstructAuxiliaryFields();
                Actor finalPlayer = finalMap.GetActorAt(1, 1);
                Check.Equal(0, finalPlayer.Personality.Memories.Count,
                    "resolved player memory leaves the pending queue after load");
                Check.Equal(1, finalPlayer.Personality.Person(attackerId).Memories.Count,
                    "player's resolved relationship history survives another save");
                Check.Equal("none", finalPlayer.Personality.Person(attackerId).Memories[0].OutcomeId,
                    "journal resolution does not give player an NPC outcome");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
