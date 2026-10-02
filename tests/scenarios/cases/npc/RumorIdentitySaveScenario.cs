using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RumorIdentitySaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/rumor-identity-save", () => TownScenarioFactory.Arena(4902,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            attacker.Faction = world.Game.GameFactions.TheBikers;
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal("a biker", fact.ReportOther, "first witness does not know the attacker");
            string path = Path.Combine(Path.GetTempPath(), "rumor-identity-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(map, witness.PersonalityIdentity);
                NpcFact savedFact = saved.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId);
                Check.Equal("a biker", savedFact.ReportOther, "faction description survives saving");
                Check.Equal(attacker.Faction.ID, savedFact.OtherFactionId, "actor faction survives saving");
                Check.Equal(attacker.PersonalityIdentity, savedFact.OtherId, "stable actor identity survives saving");
                Check.Equal("a biker", savedFact.Retell(saved.PersonalityIdentity, 1, 70).ReportOther,
                    "saved hearsay keeps its original description");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
