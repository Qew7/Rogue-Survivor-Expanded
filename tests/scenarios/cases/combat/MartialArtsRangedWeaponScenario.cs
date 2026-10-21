using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;

static class MartialArtsRangedWeaponScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/martial-arts-ranged-weapon", () => TownScenarioFactory.Arena(4781,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(player, new Point(1, 1));
            world.SetPlayer(player);
            Actor zombie = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(zombie, new Point(2, 1));
            Attack unarmedAttack = player.CurrentMeleeAttack;
            int baseHit = world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).HitValue;
            int baseDamage = world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).DamageValue;
            world.Game.SkillUpgrade(player, Skills.IDs.MARTIAL_ARTS);
            int skilledHit = world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).HitValue;
            int skilledDamage = world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).DamageValue;
            Check.Equal(true, skilledHit > baseHit && skilledDamage > baseDamage,
                "martial arts improves unarmed attack");

            ItemRangedWeapon gun = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            player.Inventory.AddAll(gun);
            gun.EquippedPart = DollPart.RIGHT_HAND;
            Check.Equal(skilledHit, world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).HitValue,
                "gun keeps martial arts accuracy");
            Check.Equal(skilledDamage, world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).DamageValue,
                "gun keeps martial arts damage");

            int ammo = gun.Ammo;
            int ap = player.ActionPoints;
            player.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("hit"), 1000, 5, 0, 0);
            zombie.CurrentDefence = new Defence(0, 0, 0);
            int hp = zombie.HitPoints;
            Check.Equal(true, world.Try(new ActionMeleeAttack(player, world.Game, zombie)),
                "melee attack is legal while holding gun");
            Check.Equal(true, player.ActionPoints < ap && zombie.HitPoints < hp,
                "melee action spends AP and damages target");
            Check.Equal(ammo, gun.Ammo, "melee attack does not fire gun");

            ItemMeleeWeapon knife = new ItemMeleeWeapon(world.Game.GameItems.COMBAT_KNIFE);
            player.Inventory.AddAll(knife);
            player.CurrentMeleeAttack = unarmedAttack;
            gun.EquippedPart = DollPart.NONE;
            knife.EquippedPart = DollPart.RIGHT_HAND;
            Check.Equal(baseHit, world.Game.Rules.ActorMeleeAttack(player, player.CurrentMeleeAttack, zombie).HitValue,
                "held melee weapon gets no martial arts bonus");
            Actor distant = SkillScenario.Actor(world, true);
            world.Map.PlaceActorAt(distant, new Point(4, 1));
            Check.Equal(false, world.Game.Rules.CanActorMeleeAttack(player, distant),
                "gun does not extend melee reach");
        });
    }
}
