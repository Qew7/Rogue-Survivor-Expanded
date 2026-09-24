using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class RangedAttackScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/ranged", () => TownScenarioFactory.Arena(4303,
            ".......", ".......", "......."), world =>
        {
            Actor shooter = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(shooter, new Point(1, 1));
            world.SetPlayer(shooter);
            Actor target = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(target, new Point(4, 1));
            Check.Equal(false, world.Game.Rules.CanActorFireAt(shooter, target), "unarmed actor cannot fire");
            ItemRangedWeapon gun = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            Check.Equal(true, shooter.Inventory.AddAll(gun), "pistol fits inventory");
            gun.EquippedPart = DollPart.RIGHT_HAND;
            shooter.CurrentRangedAttack = Attack.RangedAttack(AttackKind.FIREARM,
                new Verb("fire"), 1000, 1000, 1000, 5, 8);
            int beforeAmmo = gun.Ammo;
            int beforeHP = target.HitPoints;
            Check.Equal(true, world.Try(new ActionRangedAttack(shooter, world.Game, target)), "shot has line of fire");
            Check.Equal(true, gun.Ammo < beforeAmmo, "shot consumes ammunition");
            Check.Equal(true, target.HitPoints < beforeHP, "shot damages target");
            world.Map.SetTileModelAt(2, 1, world.Game.GameTiles.WALL_BRICK);
            Check.Equal(false, world.Game.Rules.CanActorFireAt(shooter, target),
                "wall blocks line of fire");
            world.Map.SetTileModelAt(2, 1, world.Game.GameTiles.FLOOR_ASPHALT);
            gun.Ammo = 0;
            Check.Equal(false, world.Game.Rules.CanActorFireAt(shooter, target), "empty pistol cannot fire");
        });
    }
}
