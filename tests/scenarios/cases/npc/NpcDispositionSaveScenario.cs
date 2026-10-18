using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcDispositionSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/disposition-save", () => TownScenarioFactory.Arena(8163,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor resident = NpcIntentSupport.Actor(world, "resident", 1, 1);
            resident.Personality = new PersonalityState();
            resident.Personality.AddTrait(new TraitInstance("kind"));
            resident.Personality.ChangeExperience(NpcPersonalityAxis.HumanLife, -9);
            resident.Personality.ChangeExperience(NpcPersonalityAxis.CareCircle, 12);
            resident.Personality.ChangeExperience(NpcPersonalityAxis.MessageTrust, -16);
            resident.Personality.ChangeExperience(NpcPersonalityAxis.Scarcity, 23);
            world.Map.LocalTime.TurnCounter = 17;
            NpcMemoryProcessor.Add(resident, world.Game.NpcContent.Personalities.Memory("starvation"),
                world.Map.LocalTime.TurnCounter, null, original.GameDiceRoller);
            int memoryDue = resident.Personality.Memories[0].ResolveTurn;
            int life = PersonalitySystem.Disposition(resident, NpcPersonalityAxis.HumanLife, world.Game.NpcContent);
            string path = Path.Combine(Path.GetTempPath(), "npc-disposition-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap;
                map.ReconstructAuxiliaryFields();
                Actor restored = map.GetActorAt(1, 1);
                Check.Equal(memoryDue, map.NextMemoryResolutionTurn,
                    "loading rebuilds the next pending memory deadline");
                Check.Equal(-9, restored.Personality.ExperienceValue(NpcPersonalityAxis.HumanLife), "life experience survives save and load");
                Check.Equal(12, restored.Personality.ExperienceValue(NpcPersonalityAxis.CareCircle), "care circle survives save and load");
                Check.Equal(-16, restored.Personality.ExperienceValue(NpcPersonalityAxis.MessageTrust), "message trust survives save and load");
                Check.Equal(23, restored.Personality.ExperienceValue(NpcPersonalityAxis.Scarcity), "scarcity survives save and load");
                Session.Restore(loaded);
                Check.Equal(life, PersonalitySystem.Disposition(restored, NpcPersonalityAxis.HumanLife, world.Game.NpcContent),
                    "faction, retained trait and experience recombine after loading");
                restored.Personality.RemoveTrait("kind");
                Check.Equal(true, PersonalitySystem.Disposition(restored, NpcPersonalityAxis.HumanLife, world.Game.NpcContent) < life,
                    "a later trait loss changes the loaded score");
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
