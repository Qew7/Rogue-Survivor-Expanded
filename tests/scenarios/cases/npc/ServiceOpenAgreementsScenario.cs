using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ServiceOpenAgreementsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/service-open-agreements", () => TownScenarioFactory.Arena(4843,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "provider", 2, 1);
            var agreement = new NpcServiceAgreement { Id = 4843, Provider = owner.PersonalityIdentity,
                Patient = Guid.NewGuid(), Shelter = owner.Location, DueTurn = 1 };
            owner.Personality.RememberService(agreement);
            Check.Equal(true, owner.Personality.HasOpenServiceAgreements, "offered service enters the active set");
            NpcServices.Expire(world.Game.NpcContent, owner);
            Check.Equal(true, owner.Personality.HasOpenServiceAgreements, "unexpired offer stays open");
            world.Map.LocalTime.TurnCounter = 1;
            NpcServices.Expire(world.Game.NpcContent, owner);
            Check.Equal(NpcServiceStatus.Refused, owner.Personality.ServiceAgreements[0].Status,
                "map deadline closes an unanswered offer");
            Check.Equal(false, owner.Personality.HasOpenServiceAgreements,
                "completed history no longer enters per-turn service scans");
            Check.Equal(1, owner.Personality.ServiceAgreements.Count, "closed offer remains in history");
            string path = Path.Combine(Path.GetTempPath(), "npc-open-services-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ScenarioWorld restored = NpcIntentSupport.Restore(world, loaded);
                Actor saved = NpcIntentSupport.Find(restored.Map, owner.PersonalityIdentity);
                Check.Equal(false, saved.Personality.HasOpenServiceAgreements,
                    "loaded closed history rebuilds the transient active indicator");
                Check.Equal(NpcServiceStatus.Refused, saved.Personality.ServiceAgreements[0].Status,
                    "save retains the terminal outcome");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }
}
