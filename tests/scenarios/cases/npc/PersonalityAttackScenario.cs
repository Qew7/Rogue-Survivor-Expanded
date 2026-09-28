using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;

static class PersonalityAttackScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-attack", () => TownScenarioFactory.Arena(4521,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor attacker = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(attacker, new Point(1, 1));
            world.SetPlayer(attacker);
            Actor target = SkillScenario.Actor(world);
            target.Personality = new PersonalityState();
            world.Map.PlaceActorAt(target, new Point(2, 1));
            Actor distant = SkillScenario.Actor(world);
            distant.Personality = new PersonalityState();
            world.Map.PlaceActorAt(distant, new Point(4, 1));
            attacker.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("hit"), 1000, 1, 0, 0);

            Check.Equal(false, world.Game.Rules.CanActorMeleeAttack(attacker, distant),
                "out-of-range attack is rejected by the game rule");
            Check.Equal(0, distant.Personality.Events.Count,
                "rejected attack produces no event");
            Check.Equal(false, world.Game.Rules.AreEnemies(attacker, target),
                "target starts friendly");
            Check.Equal(true, world.Try(new ActionMeleeAttack(attacker, world.Game, target)),
                "unexpected attack is performed");
            Check.Equal(1, target.Personality.Memories.Count,
                "unexpected attack creates a survivor memory");
            Check.Equal("survived_attack", target.Personality.Memories[0].Id,
                "correct memory is triggered");
            Check.Equal(true, world.Game.Rules.AreEnemies(attacker, target),
                "attack begins an ongoing fight");
            int observations = target.Personality.Events.Count;
            attacker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionMeleeAttack(attacker, world.Game, target)),
                "second attack is performed during same fight");
            Check.Equal(observations, target.Personality.Events.Count,
                "routine blows in an ongoing fight do not fill the journal");
            Check.Equal(1, target.Personality.Memories.Count,
                "ongoing fight does not start another survivor memory");
        });
    }
}
