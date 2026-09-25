using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class XpdLegacyFactionClaimScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/legacy-faction-claim", () => TownScenarioFactory.Arena(4434,
            ".....", ".....", "....."), world =>
        {
            Actor oldOwner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "old owner", false, false, 0);
            XpdBase obsolete = new XpdBase(oldOwner, new[] { new Point(1, 1) });
            typeof(XpdBase).GetField("m_GroupLeader", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(obsolete, null); // Shape of faction claims written by earlier builds.
            world.Map.AddXpdBase(obsolete);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            leader.AddFollower(follower);
            world.Map.AddXpdBase(new XpdBase(leader, new[] { new Point(3, 1) }));

            string path = Path.Combine(Path.GetTempPath(), "legacy-claim-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal(null, loaded.XpdBaseAt(new Point(1, 1)),
                    "old faction claim is released on load");
                Check.Equal(true, loaded.XpdBaseAt(new Point(3, 1)) != null,
                    "valid group claim remains on load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
