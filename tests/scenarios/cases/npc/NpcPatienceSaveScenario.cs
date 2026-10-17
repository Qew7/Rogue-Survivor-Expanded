using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPatienceSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-save", () => TownScenarioFactory.Arena(8195,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor resident = NpcIntentSupport.Actor(world, "resident", 1, 1, "patient");
            Guid id = resident.PersonalityIdentity;
            FieldInfo values = typeof(PersonalityState).GetField("m_ExperienceValues",
                BindingFlags.NonPublic | BindingFlags.Instance);
            values.SetValue(resident.Personality, new[] { 3, 4, 5, 6 });
            resident.Personality.ChangeFrustration(12, 37);
            string path = Path.Combine(Path.GetTempPath(), "npc-patience-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(map, id);
                Session.Restore(loaded);
                Check.Equal(0, restored.Personality.ExperienceValue(NpcPersonalityAxis.Patience),
                    "an older four-axis save starts with neutral patience experience");
                Check.Equal(37, restored.Personality.Frustration(12),
                    "frustration survives a save and load");
                Check.Equal(34, restored.Personality.Frustration(42),
                    "frustration fades as map turns pass after loading");
                int patient = PersonalitySystem.Disposition(restored, NpcPersonalityAxis.Patience, world.Game.NpcContent);
                restored.Personality.ChangeExperience(NpcPersonalityAxis.Patience, -12);
                Check.Equal(6, restored.Personality.ExperienceValue(NpcPersonalityAxis.Scarcity),
                    "growing the old array keeps the existing disposition values");
                Check.Equal(patient - 12,
                    PersonalitySystem.Disposition(restored, NpcPersonalityAxis.Patience, world.Game.NpcContent),
                    "experience alters the loaded NPC's patience");
                BinarySaveStore.Save(path, loaded);
                Session reloaded = BinarySaveStore.Load<Session>(path);
                Actor again = NpcIntentSupport.Find(reloaded.World[0, 0].EntryMap, id);
                Check.Equal(-12, again.Personality.ExperienceValue(NpcPersonalityAxis.Patience),
                    "the new patience value survives another save and load");
                Check.Equal(37, again.Personality.Frustration(12),
                    "a second save keeps the unresolved frustration");
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
