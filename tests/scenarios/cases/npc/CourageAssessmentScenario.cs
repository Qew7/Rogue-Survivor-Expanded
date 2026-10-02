using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class CourageAssessmentScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/courage-assessment", () => TownScenarioFactory.Arena(5803,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 2, 2);
            int threat;
            int civilian = NpcCourage.Resolve(world.Game, owner, null, out threat);
            owner.Faction = world.Game.GameFactions.TheArmy;
            Check.Equal(true, NpcCourage.Resolve(world.Game, owner, null, out threat) > civilian,
                "army training supplies a stronger faction base");
            owner.Faction = world.Game.GameFactions.TheCivilians;

            world.Game.SkillUpgrade(owner, Skills.IDs.FIREARMS);
            int missingKit = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, missingKit < civilian, "shooting training without a gun costs confidence");
            var pistol = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            Check.Equal(true, owner.Inventory.AddAll(pistol), "pistol fits");
            pistol.EquippedPart = DollPart.RIGHT_HAND;
            int loaded = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, loaded > civilian, "loaded pistol makes shooting skill relevant");
            pistol.Ammo = 0;
            int empty = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, empty < loaded, "empty pistol removes shooting confidence");

            owner.Personality.AddTrait(new TraitInstance("maniac"));
            int soundMind = NpcCourage.Resolve(world.Game, owner, null, out threat);
            owner.Sanity = 0;
            int manic = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, manic > soundMind, "maniac grows bolder as sanity falls");
            owner.Personality = new PersonalityState();
            int shaken = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, shaken < manic, "ordinary civilian loses nerve at the same sanity");

            Actor companion = NpcIntentSupport.Actor(world, "companion", 2, 1);
            owner.AddFollower(companion);
            int supported = NpcCourage.Resolve(world.Game, owner, null, out threat);
            companion.HitPoints = 1;
            Check.Equal(true, NpcCourage.Resolve(world.Game, owner, null, out threat) < supported,
                "wounded companion no longer inspires the group");
            companion.HitPoints = world.Game.Rules.ActorMaxHPs(companion);
            companion.Activity = Activity.FLEEING;
            int fearfulGroup = NpcCourage.Resolve(world.Game, owner, null, out threat);
            Check.Equal(true, fearfulGroup < supported, "visible companion panic spreads to the group");

            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            world.Place(zombie, 3, 2);
            var enemies = new List<Percept> { new Percept(zombie, 0, zombie.Location) };
            int oneEnemy = NpcCourage.Resolve(world.Game, owner, enemies, out threat);
            Check.Equal(true, threat > 0 && oneEnemy < fearfulGroup, "nearby enemy reduces resolve");
            Actor second = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "second zombie", false, false, 0);
            world.Place(second, 3, 3);
            enemies.Add(new Percept(second, 0, second.Location));
            Check.Equal(true, NpcCourage.Resolve(world.Game, owner, enemies, out threat) < oneEnemy,
                "multiple attackers accumulate danger");
        });
    }
}
