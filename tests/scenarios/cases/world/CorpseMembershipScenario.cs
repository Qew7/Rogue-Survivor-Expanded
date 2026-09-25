using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class CorpseMembershipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/corpse-membership", () =>
            TownScenarioFactory.Arena(4588, "...", "...", "..."), world =>
        {
            Actor dead = SkillScenario.Actor(world);
            Corpse corpse = new Corpse(dead, 5, 5, 0, 0, 1);
            world.Map.AddCorpseAt(corpse, new Point(1, 1));
            Check.Equal(true, world.Map.HasCorpse(corpse), "added corpse belongs to map");
            world.Map.MoveCorpseTo(corpse, new Point(2, 1));
            Check.Equal(true, world.Map.HasCorpse(corpse), "moved corpse remains in map");
            Check.Equal(1, world.Map.GetCorpsesAt(2, 1).Count,
                "moved corpse appears at destination");
            string path = Path.Combine(Path.GetTempPath(), "corpse-membership-" +
                Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Corpse reloaded = loaded.GetCorpsesAt(2, 1)[0];
                Check.Equal(true, loaded.HasCorpse(reloaded),
                    "membership rebuilds after load");
                loaded.RemoveCorpse(reloaded);
                Check.Equal(false, loaded.HasCorpse(reloaded),
                    "removed corpse no longer belongs to map");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
