using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalitySaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-save", () => TownScenarioFactory.Arena(4513,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            world.Place(actor, 1, 1);
            actor.Personality.Remember(new ObservedEvent("raid", 12, "raider", null, false));
            string path = Path.Combine(Path.GetTempPath(), "personality-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map restoredMap = loaded.World[0, 0].EntryMap;
                restoredMap.ReconstructAuxiliaryFields();
                Actor restored = restoredMap.GetActorAt(1, 1);
                Check.Equal(3, restored.Personality.Traits.Count, "traits survive save and load");
                Check.Equal(actor.Personality.Memories.Count, restored.Personality.Memories.Count,
                    "pending memories survive save and load");
                Check.Equal("raid", restored.Personality.Events[0].Kind,
                    "witnessed event survives save and load");
                Check.Equal(true, loaded.GamePreset.NpcPersonalitiesEnabled,
                    "preset option survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
