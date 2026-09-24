using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;

static class MeleeAttackScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/melee", () => TownScenarioFactory.Arena(4302,
            ".....", ".....", "....."), world =>
        {
            Actor attacker = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(attacker, new Point(1, 1));
            world.SetPlayer(attacker);
            Actor target = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(target, new Point(2, 1));
            Check.Equal(true, attacker.Faction.IsEnemyOf(target.Faction), "target is hostile");
            attacker.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("hit"), 1000, 5, 0, 0);
            int ap = attacker.ActionPoints;
            int hp = target.HitPoints;
            Check.Equal(true, world.Try(new ActionMeleeAttack(attacker, world.Game, target)), "adjacent attack legal");
            Check.Equal(true, attacker.ActionPoints < ap, "attack costs action points");
            Check.Equal(true, target.HitPoints < hp, "melee damages target");
            Actor distant = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(distant, new Point(4, 1));
            Check.Equal(false, world.Game.Rules.CanActorMeleeAttack(attacker, distant),
                "melee cannot reach a distant actor");
        });
    }
}
