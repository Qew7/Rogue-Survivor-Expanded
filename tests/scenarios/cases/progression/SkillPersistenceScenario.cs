using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillPersistenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("progression/skill-persists", () => TownScenarioFactory.Arena(4320,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(2, 1));
            int baseHP = world.Game.Rules.ActorMaxHPs(actor);
            world.Game.SkillUpgrade(actor, Skills.IDs.TOUGH);
            int boostedHP = world.Game.Rules.ActorMaxHPs(actor);
            Check.Equal(true, boostedHP > baseHP, "upgrade boosts maximum health");
            string path = Path.Combine(Path.GetTempPath(), "rogue-skill-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Actor restored = loaded.GetActorAt(2, 1);
                Check.Equal(1, restored.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.TOUGH),
                    "skill level survives reload");
                Check.Equal(boostedHP, world.Game.Rules.ActorMaxHPs(restored),
                    "skill effect survives reload");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
