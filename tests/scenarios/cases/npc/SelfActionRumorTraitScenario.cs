using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class SelfActionRumorTraitScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/self-action-rumor-trait", () => TownScenarioFactory.Arena(4912,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 2, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 6, 1);
            Actor player = NpcIntentSupport.Player(world, 8, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker,
                world.Map, victim.Location.Position, 0));
            NpcFact attack = attacker.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            NpcFact victimReport = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            NpcFact witnessed = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "attack");
            Check.Equal(null, NpcConversation.Rumor(world.Game, attacker, player),
                "attacker without a fitting trait does not offer their own attack as a rumor");
            world.Place(attacker, 5, 0); world.Place(victim, 6, 0); world.Place(witness, 7, 0);
            Check.Equal(false, new ActionNpcTell(attacker, world.Game, listener, attack).IsLegal(),
                "manual retelling also rejects an unmotivated confession");
            attacker.Personality.AddTrait(new TraitInstance("cruel"));
            Check.Equal(true, new ActionNpcTell(attacker, world.Game, listener, attack).IsLegal(),
                "cruel attacker is willing to boast about the attack");
            Check.Equal(attack.EventId, NpcConversation.Rumor(world.Game, attacker, player).EventId,
                "player conversation can now select the actor's own attack");
            Check.Equal(true, world.Try(new ActionNpcTell(victim, world.Game, listener, victimReport)),
                "victim can tell about the attack without a self-report trait");
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, listener, witnessed)),
                "eyewitness can tell about someone else's attack");

            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 2);
            PersonalitySystem.Report(world.Game, new SignificantEvent("shared_food", helper, victim,
                world.Map, helper.Location.Position, 0));
            NpcFact aid = helper.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            world.Place(helper, 5, 1);
            Check.Equal(false, new ActionNpcTell(helper, world.Game, listener, aid).IsLegal(),
                "helper without a fitting trait does not promote their own aid");
            helper.Personality.AddTrait(new TraitInstance("kind"));
            Check.Equal(true, world.Try(new ActionNpcTell(helper, world.Game, listener, aid)),
                "kind helper can tell about their own aid");

            Actor thief = NpcIntentSupport.Actor(world, "thief", 2, 0);
            PersonalitySystem.Report(world.Game, new SignificantEvent("base_theft", thief, victim,
                world.Map, thief.Location.Position, 0));
            NpcFact theft = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            world.Place(thief, 5, 2);
            Check.Equal(false, new ActionNpcTell(thief, world.Game, listener, theft).IsLegal(),
                "ordinary thief does not confess a theft");
            thief.Personality.AddTrait(new TraitInstance("rebellious"));
            Check.Equal(true, new ActionNpcTell(thief, world.Game, listener, theft).IsLegal(),
                "rebellious thief may boast about stealing");
        });
    }
}
