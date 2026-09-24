using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;

// Compare the actual rule result before and after gaining a skill level.
static class SkillScenario
{
    public static void AssertCoverage()
    {
        foreach (string id in Enum.GetNames(typeof(Skills.IDs)))
        {
            if (id.StartsWith("_", StringComparison.Ordinal)) continue;
            string name = "skill/" + id.ToLowerInvariant().Replace('_', '-');
            Check.Equal(true, ScenarioRunner.Contains(name), "missing scenario for " + id);
        }
    }

    public static void Register(string name, Skills.IDs id,
        Func<ScenarioWorld, Actor, double> effect, bool undead = false, bool decreases = false,
        Func<ScenarioWorld, Actor, double> secondEffect = null, bool secondDecreases = false)
    {
        ScenarioRunner.Add("skill/" + name, () => TownScenarioFactory.Create(4201, false), world =>
        {
            Actor actor = Actor(world, undead);
            int level = actor.Sheet.SkillTable.GetSkillLevel((int)id);
            double before = effect(world, actor);
            double secondBefore = secondEffect == null ? 0 : secondEffect(world, actor);
            world.Game.SkillUpgrade(actor, id);
            Check.Equal(level + 1, actor.Sheet.SkillTable.GetSkillLevel((int)id), "skill level increased");
            double after = effect(world, actor);
            Check.Equal(true, decreases ? after < before : after > before,
                id + " changes its game rule: before " + before + ", after " + after);
            if (secondEffect != null)
            {
                double secondAfter = secondEffect(world, actor);
                Check.Equal(true, secondDecreases ? secondAfter < secondBefore : secondAfter > secondBefore,
                    id + " changes its second game rule: before " + secondBefore + ", after " + secondAfter);
            }
        });
    }

    public static Actor Actor(ScenarioWorld world, bool undead = false)
    {
        Actor actor = undead ?
            new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads, "zombie", false, false, 0) :
            new Actor(world.Game.GameActors.MaleCivilian, world.Game.GameFactions.TheCivilians, "civilian", false, false, 0);
        for (int y = 0; y < world.Map.Height; y++)
            for (int x = 0; x < world.Map.Width; x++)
                if (world.Map.GetTileAt(x, y).Model.IsWalkable && world.Map.GetActorAt(x, y) == null)
                {
                    world.Place(actor, x, y);
                    return actor;
                }
        throw new InvalidOperationException("No free floor tile for skill actor");
    }

    public static double MeleeDamage(ScenarioWorld world, Actor actor)
    {
        Attack attack = world.Game.Rules.ActorMeleeAttack(actor, actor.CurrentMeleeAttack, null);
        return attack.DamageValue;
    }

    public static double MeleeHit(ScenarioWorld world, Actor actor)
    {
        Attack attack = world.Game.Rules.ActorMeleeAttack(actor, actor.CurrentMeleeAttack, null);
        return attack.HitValue;
    }

    public static double RangedDamage(ScenarioWorld world, Actor actor, AttackKind kind)
    {
        Attack baseAttack = Attack.RangedAttack(kind, new Verb("fire"), 50, 50, 50, 10, 8);
        return world.Game.Rules.ActorRangedAttack(actor, baseAttack, 4, null).DamageValue;
    }

    public static double RangedHit(ScenarioWorld world, Actor actor, AttackKind kind)
    {
        Attack baseAttack = Attack.RangedAttack(kind, new Verb("fire"), 50, 50, 50, 10, 8);
        return world.Game.Rules.ActorRangedAttack(actor, baseAttack, 4, null).HitValue;
    }

    public static double TrapChance(ScenarioWorld world, Actor actor)
    {
        return world.Game.Rules.GetTrapTriggerChance(new ItemTrap(world.Game.GameItems.BEAR_TRAP), actor);
    }
}
