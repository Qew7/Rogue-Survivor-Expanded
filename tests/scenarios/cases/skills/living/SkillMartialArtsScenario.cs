using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Engine.Items;

static class SkillMartialArtsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("skill/martial-arts", () => TownScenarioFactory.Create(4201, false), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            double unarmed = SkillScenario.MeleeDamage(world, actor);
            world.Game.SkillUpgrade(actor, Skills.IDs.MARTIAL_ARTS);
            Check.Equal(true, SkillScenario.MeleeDamage(world, actor) > unarmed,
                "unarmed strikes gain damage");
            ItemMeleeWeapon knife = new ItemMeleeWeapon(world.Game.GameItems.COMBAT_KNIFE);
            actor.Inventory.AddAll(knife);
            knife.EquippedPart = DollPart.RIGHT_HAND;
            Check.Equal(unarmed, SkillScenario.MeleeDamage(world, actor),
                "martial arts bonus does not apply while holding a weapon");
        });
    }
}
