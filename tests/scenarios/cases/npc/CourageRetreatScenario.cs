using System;
using System.Collections.Generic;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class CourageRetreatScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/courage-retreat", () => TownScenarioFactory.Arena(5801,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 2, "timid");
            Actor zombie = new Actor(world.Game.GameActors.Zombie, world.Game.GameFactions.TheUndeads,
                "zombie", false, false, 0);
            zombie.CurrentMeleeAttack = Attack.MeleeAttack(new Verb("bite"), 100, 3, 0, 0);
            world.Place(zombie, 4, 2);
            owner.HitPoints = 1;
            owner.StaminaPoints = 1;
            int threat;
            var visible = new List<Percept> { new Percept(zombie, 0, zombie.Location) };
            Check.Equal(true, NpcCourage.ImmediateMortalThreat(world.Game, owner, visible),
                "one nearby blow could kill the wounded civilian");
            int frightened = NpcCourage.Resolve(world.Game, owner, visible, out threat);
            Check.Equal(true, threat >= 20 && frightened <= -45, "wounded and exhausted civilian judges adjacent zombie mortal");
            int healthyHp = world.Game.Rules.ActorMaxHPs(owner);
            owner.HitPoints = healthyHp;
            owner.StaminaPoints = world.Game.Rules.ActorMaxSTA(owner);
            int healthy = NpcCourage.Resolve(world.Game, owner, visible, out threat);
            Check.Equal(true, healthy > frightened, "health and stamina raise resolve");
            owner.HitPoints = 1;
            owner.StaminaPoints = 1;
            var before = owner.Location.Position;
            owner.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(owner), "civilian executes a real retreat action");
            Check.Equal(true, owner.Location.Position != before, "panic moves the civilian away");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "fled_in_fear"), "completed retreat enters the actor history");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "fled_in_fear"), "eyewitness sees the actual retreat");
            Check.Equal(true, owner.Personality.Memories.Any(m => m.Id == "frightened_escape"), "fright becomes a resolvable memory");
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "fled_in_fear");
            Check.Equal(true, fact != null && fact.OtherId == zombie.PersonalityIdentity, "witness retains the threat attribution");
            int due = owner.Personality.Memories.First(m => m.Id == "frightened_escape").ResolveTurn;
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, owner.Personality.HasTrait("traumatized"), "timid survivor can develop lasting trauma");
            Check.Equal(false, owner.Personality.Memories.Any(m => m.Id == "frightened_escape"), "resolved fear no longer suppresses current resolve");
            Check.Equal(false, NpcCourage.CanFear(zombie), "undead never use civilian fear");
            Check.Equal(false, NpcCourage.CanFear(NpcIntentSupport.Player(world, 0, 0)), "player decides for themselves");
        });
    }
}
