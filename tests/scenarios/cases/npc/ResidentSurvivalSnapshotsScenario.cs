using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class ResidentSurvivalSnapshotsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resident-survival-snapshots", () => TownScenarioFactory.Arena(5811,
            ".........", ".........", ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 2, "timid");
            ResidentRecord record = Session.Get.ResidentRecords.Register(owner);
            Check.Equal(null, record.ThreatSnapshot, "an unthreatened resident has no threat snapshot");
            Check.Equal(null, record.DeathSnapshot, "a living resident has no death snapshot");
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            world.Place(zombie, 4, 2);
            Session.Get.ResidentRecords.RecordThreat(owner, world.Game.Rules, 0, zombie, 4, 19, 10, false);
            Check.Equal(null, record.ThreatSnapshot, "minor danger does not consume the one threat slot");
            owner.HitPoints = owner.StaminaPoints = 1;
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(owner), "real NPC assesses the nearby zombie");
            ResidentSurvivalSnapshot threat = record.ThreatSnapshot;
            Check.Equal(true, threat != null, "first serious threat is recorded");
            Check.Equal(1, threat.HitPoints, "threat snapshot keeps health before combat");
            Check.Equal(1, threat.Stamina, "threat snapshot keeps stamina before retreat");
            Check.Equal(true, threat.Threat >= 20 || threat.Mortal, "only serious danger is retained");
            Check.Equal(zombie.PersonalityIdentity, threat.OtherId, "visible threat is identified");
            Check.Equal(true, threat.EnemyDistance > 0, "distance to visible enemy is retained");
            Session.Get.ResidentRecords.RecordThreat(owner, world.Game.Rules, 1, zombie, 1, 100, -100, true);
            Check.Same(threat, record.ThreatSnapshot, "later threats do not overwrite the first snapshot");

            owner.FoodPoints = 0;
            owner.SleepPoints = 0;
            owner.Sanity = 0;
            owner.IsSleeping = true;
            world.Game.KillActor(zombie, owner, "hit", false);
            ResidentSurvivalSnapshot death = record.DeathSnapshot;
            Check.Equal(true, death != null, "death state survives removal without a corpse");
            Check.Equal(true, death.Starving && death.Exhausted && death.Insane && death.Sleeping,
                "death snapshot preserves actual need and sleep statuses");
            Check.Equal("hit", death.Reason, "death snapshot keeps the actual reason");
            Check.Equal(zombie.PersonalityIdentity, death.OtherId, "death snapshot keeps the killer identity");
            Check.Same(threat, record.ThreatSnapshot, "death cannot replace the first threat snapshot");

            string path = Path.Combine(Path.GetTempPath(), "survival-snapshots-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                ResidentRecord archived = RecordsReader.Residents(RecordsReader.Load(path))
                    .Find(r => r.Identity == owner.PersonalityIdentity);
                Check.Equal(true, archived != null && archived.ThreatSnapshot != null && archived.DeathSnapshot != null,
                    "archive-only reader restores both snapshots without the actor");
                Check.Equal(threat.Threat, archived.ThreatSnapshot.Threat, "threat assessment survives save and load");
                Check.Equal(death.Speed, archived.DeathSnapshot.Speed, "effective speed survives save and load");
                Check.Equal("hit", archived.DeathSnapshot.Reason, "death cause survives save and load");
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                ResidentRecord restored = null;
                foreach (ResidentRecord resident in loaded.ResidentRecords.Residents)
                    if (resident.Identity == owner.PersonalityIdentity) restored = resident;
                Check.Equal(true, restored != null && restored.ThreatSnapshot != null && restored.DeathSnapshot != null,
                    "full world load restores both snapshots");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
