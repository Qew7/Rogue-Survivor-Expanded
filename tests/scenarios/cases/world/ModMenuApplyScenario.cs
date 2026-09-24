using System;
using djack.RogueSurvivor.Engine;

static class ModMenuApplyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("mods/menu-apply",
            () => TownScenarioFactory.Create(4410, false),
            world =>
            {
                string originalName = world.Game.GameActors.Skeleton.Name;
                ModInfo mod = ModCatalog.Discover("mods")[0];
                try
                {
                    ModCatalog.Select(mod);
                    Check.Call(world.Game, "ReloadModResources");
                    Check.Equal("rat", world.Game.GameActors.Skeleton.Name,
                        "applying the menu selection reloads modded actor data");
                    ModCatalog.Select();
                    Check.Call(world.Game, "ReloadModResources");
                    Check.Equal(originalName, world.Game.GameActors.Skeleton.Name,
                        "disabling the mod restores original actor data");
                }
                finally { ModCatalog.Select(); }
            });
    }
}
