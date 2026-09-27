using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalitySaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-save", () => TownScenarioFactory.Arena(4513,
            "...", "...", "..."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            world.Place(actor, 1, 1);
            actor.Personality = new PersonalityState();
            int magazineId = world.Game.GameItems.MAGAZINE.ID;
            actor.Personality.AddTrait(new TraitInstance("likes_items", magazineId));
            actor.Personality.AddMemory(new MemoryInstance("witnessed_murder", 12, 4321, "victim", true));
            actor.Personality.Remember(new ObservedEvent("murder", 13, "victim", "killer", false, true));
            string path = Path.Combine(Path.GetTempPath(), "personality-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map restoredMap = loaded.World[0, 0].EntryMap;
                restoredMap.ReconstructAuxiliaryFields();
                Actor restored = restoredMap.GetActorAt(1, 1);
                Check.Equal(1, restored.Personality.Traits.Count, "trait survives save and load");
                Check.Equal("likes_items", restored.Personality.Traits[0].Id, "trait identity survives");
                Check.Equal(magazineId, restored.Personality.Traits[0].ItemModelId,
                    "parameterized item preference survives");
                Check.Equal(35, PersonalitySystem.Bias(restored, DecisionKind.Item,
                    new ItemEntertainment(world.Game.GameItems.MAGAZINE)),
                    "restored preference still affects item decisions");
                Check.Equal(1, restored.Personality.Memories.Count, "pending memory survives");
                Check.Equal("witnessed_murder", restored.Personality.Memories[0].Id,
                    "memory identity survives");
                Check.Equal(12, restored.Personality.Memories[0].StartTurn, "memory start survives");
                Check.Equal(4321, restored.Personality.Memories[0].ResolveTurn, "deadline survives");
                Check.Equal("victim", restored.Personality.Memories[0].Subject, "memory subject survives");
                Check.Equal(true, restored.Personality.Memories[0].RelatedToSubject,
                    "relationship captured in a pending memory survives");
                Check.Equal(1, restored.Personality.Events.Count, "witnessed event survives");
                Check.Equal("murder", restored.Personality.Events[0].Kind, "event kind survives");
                Check.Equal(13, restored.Personality.Events[0].Turn, "event time survives");
                Check.Equal("victim", restored.Personality.Events[0].Subject, "event subject survives");
                Check.Equal("killer", restored.Personality.Events[0].Other, "event other actor survives");
                Check.Equal(false, restored.Personality.Events[0].Direct, "witness role survives");
                Check.Equal(true, restored.Personality.Events[0].RelatedToSubject,
                    "relationship at the time of an event survives");
                Check.Equal(true, loaded.GamePreset.NpcPersonalitiesEnabled,
                    "preset option survives save and load");

                Session.Restore(loaded);
                restoredMap.LocalTime.TurnCounter = 4321;
                PersonalitySystem.ResolveDue(world.Game, restoredMap);
                Check.Equal(0, restored.Personality.Memories.Count,
                    "pending memory resolves after the saved game is restored");
                Check.Equal(1, restored.Sheet.SkillTable.GetSkillLevel(
                    (int)djack.RogueSurvivor.Gameplay.Skills.IDs.STRONG_PSYCHE),
                    "restored memory still grants its fallback skill");
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
