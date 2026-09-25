using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Generators;

static class GamePresetScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/game-presets", () => TownScenarioFactory.Arena(4460,
            ".....", ".....", "....."), world =>
        {
            GamePreset standard = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            GamePreset vintage = GamePreset.BuiltIn(GameMode.GM_VINTAGE);
            GamePreset expanded = GamePreset.BuiltIn(GameMode.GM_XPD);
            Check.Equal(false, standard.Corpses, "standard has no corpses");
            Check.Equal(true, standard.ImmediateZombification, "standard zombifies immediately");
            Check.Equal(true, vintage.Zombified, "vintage spawns zombified humans");
            Check.Equal(false, vintage.Evolution, "vintage has no evolution");
            Check.Equal(true, expanded.Bases, "expanded enables bases");
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, BaseTownGenerator.DEFAULT_PARAMS);
            Session.Get.GameMode = GameMode.GM_VINTAGE;
            Actor vintageSpawn = generator.CreateNewUndead(0);
            Check.Equal(true, vintageSpawn.Model == world.Game.GameActors.MaleZombified ||
                vintageSpawn.Model == world.Game.GameActors.FemaleZombified,
                "vintage generator creates only zombified humans");

            GamePreset custom = standard.Copy();
            custom.Name = "CUSTOM ONE";
            custom.Bases = true;
            custom.Infection = true;
            custom.HungerThreshold = 75;
            custom.SleepThreshold = 75;
            custom.SanityThreshold = 75;
            custom.RotThreshold = 75;
            custom.CorpseDecayPercent = 200;
            custom.InfectionRatePercent = 200;
            custom.InfectionWeakThreshold = 15;
            custom.InfectionEffectRatePercent = 200;
            custom.Options = RogueGame.Options;
            custom.HasOptions = true;
            Session.Get.GamePreset = custom;
            custom.Skeletons = false;
            custom.Shamblers = false;
            custom.ZombieMasters = false;
            custom.Zombified = false;
            custom.RatZombies = true;
            Session.Get.GamePreset = custom;
            Check.Same(world.Game.GameActors.RatZombie,
                generator.CreateNewUndead(0).Model, "configured zombie type controls generator");
            Actor infector = SkillScenario.Actor(world, true);
            Check.Equal(20, Rules.InfectionForDamage(infector, 10), "infection rate changes real bite rule");
            Check.Equal(true, Rules.CorpseDecayPerTurn(null) > 0, "corpse decay remains active");
            float fastDecay = Rules.CorpseDecayPerTurn(null);
            int frequentSymptoms = world.Game.Rules.InfectionEffectTriggerChance1000(50);
            custom.Skeletons = true;
            custom.Shamblers = true;
            custom.ZombieMasters = true;
            custom.Zombified = false;
            Session.Get.GamePreset = custom;
            Actor actor = SkillScenario.Actor(world);
            actor.FoodPoints = Rules.FOOD_HUNGRY_LEVEL + 1;
            actor.SleepPoints = Rules.SLEEP_SLEEPY_LEVEL + 1;
            Check.Equal(true, world.Game.Rules.IsActorHungry(actor), "custom hunger threshold changes rule");
            Check.Equal(true, world.Game.Rules.IsActorSleepy(actor), "custom sleep threshold changes rule");
            Check.Equal(true, Session.Get.GamePreset.Bases, "custom bases enabled outside expanded mode");
            Session.Get.GamePreset = standard;
            Check.Equal(true, fastDecay > Rules.CorpseDecayPerTurn(null), "corpse decay rate changes rule");
            Check.Equal(true, frequentSymptoms > world.Game.Rules.InfectionEffectTriggerChance1000(50),
                "infection symptom rate changes rule");
            Check.Equal(false, world.Game.Rules.IsActorHungry(actor), "standard hunger threshold restored");
            Check.Equal(false, world.Game.Rules.IsActorSleepy(actor), "standard sleep threshold restored");

            custom.HungerThreshold = 101;
            bool invalid = false;
            try { custom.Validate(); }
            catch (ArgumentException) { invalid = true; }
            Check.Equal(true, invalid, "out of range threshold rejected");
            custom.HungerThreshold = 75;
            custom.InfectionWeakThreshold = 40;
            invalid = false;
            try { custom.Validate(); }
            catch (ArgumentException) { invalid = true; }
            Check.Equal(true, invalid, "infection thresholds must remain ordered");
            custom.InfectionWeakThreshold = 15;

            string path = Path.Combine(Path.GetTempPath(), "game-presets-" + Guid.NewGuid().ToString("N"));
            try
            {
                GamePresetCollection collection = new GamePresetCollection();
                collection.AddOrReplace(custom);
                collection.Save(path);
                GamePresetCollection loaded = GamePresetCollection.Load(path);
                Check.Equal(1, loaded.Presets.Count, "custom preset saved");
                Check.Equal(75, loaded.Presets[0].HungerThreshold, "custom values survive reload");
                Session.Get.GamePreset = loaded.Presets[0];
                BinarySaveStore.Save(path + ".session", Session.Get);
                Session loadedSession = BinarySaveStore.Load<Session>(path + ".session");
                Check.Equal("CUSTOM ONE", loadedSession.GamePreset.Name, "game save retains preset");
                Check.Equal(true, loadedSession.GamePreset.Bases, "game save retains base rule");
                Session.Get.GameMode = GameMode.GM_VINTAGE;
                typeof(Session).GetField("m_GamePreset", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Session.Get, null);
                BinarySaveStore.Save(path + ".legacy", Session.Get);
                Session oldSession = BinarySaveStore.Load<Session>(path + ".legacy");
                Check.Equal(false, oldSession.GamePreset.Evolution,
                    "save without preset reconstructs vintage rules");
            }
            finally
            {
                foreach (string file in new[] { path, path + ".bak", path + ".session", path + ".session.bak",
                    path + ".legacy", path + ".legacy.bak" })
                    if (File.Exists(file)) File.Delete(file);
            }
        });
    }
}
