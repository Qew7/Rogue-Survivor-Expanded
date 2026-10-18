using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class ImmediateZombificationCorpseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/immediate-zombification-corpse", () => TownScenarioFactory.Arena(8031,
            ".........", ".........", ".........", ".........", "........."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_CORPSES_INFECTION);
            preset.ImmediateZombification = true;
            preset.Infection = false;
            Session.Get.GamePreset = preset;

            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", false, false, 0);
            world.Place(player, 8, 4);
            world.SetPlayer(player);

            Actor zombie = NewZombie(world, 1, 1);
            Actor victim = NewVictim(world, 2, 1);
            world.Game.DoMeleeAttack(zombie, victim);
            Check.Equal(true, victim.IsDead, "zombie kills the civilian");
            Check.Equal(true, world.Map.GetActorAt(2, 1).Model.Abilities.IsUndead,
                "victim immediately rises as a zombie");
            Check.Equal(null, world.Map.GetCorpsesAt(new Point(2, 1)),
                "immediate zombification consumes the civilian's corpse");

            preset.ImmediateZombification = false;
            Session.Get.GamePreset = preset;
            zombie = NewZombie(world, 1, 3);
            victim = NewVictim(world, 2, 3);
            world.Game.DoMeleeAttack(zombie, victim);
            Check.Equal(true, victim.IsDead, "second zombie kills the civilian");
            Check.Equal(null, world.Map.GetActorAt(2, 3),
                "without immediate zombification no zombie rises yet");
            Check.Equal(true, world.Map.GetCorpsesAt(new Point(2, 3)) != null &&
                world.Map.GetCorpsesAt(new Point(2, 3))[0].DeadGuy == victim,
                "delayed zombification leaves a corpse to rise later");
        });
    }

    static Actor NewZombie(ScenarioWorld world, int x, int y)
    {
        Actor actor = new Actor(world.Game.GameActors.Zombie,
            world.Game.GameFactions.TheUndeads, "zombie", false, false, 0);
        actor.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("bite"), 1000, 100, 0, 0);
        world.Place(actor, x, y);
        return actor;
    }

    static Actor NewVictim(ScenarioWorld world, int x, int y)
    {
        Actor actor = new Actor(world.Game.GameActors.MaleCivilian,
            world.Game.GameFactions.TheCivilians, "victim", false, false, 0);
        actor.HitPoints = 1;
        world.Place(actor, x, y);
        return actor;
    }
}
