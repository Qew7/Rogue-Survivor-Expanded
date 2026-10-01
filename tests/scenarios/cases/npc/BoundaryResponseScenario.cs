using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class BoundaryResponseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/boundary-response", () => TownScenarioFactory.Arena(4663, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "lawful");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1, "rebellious");
            owner.Personality.Knowledge.See(target, 0);
            NpcKnownPerson belief = owner.Personality.Knowledge.Person(target.PersonalityIdentity);
            belief.Violation = belief.ViolationConfidence = 100;
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "boundary_defied"), "warning does not choose its recipient's answer");
            NpcIntentSupport.Turn(world, target);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "boundary_defied"), "rebellious recipient physically rejects the boundary");
            Check.Equal(100, belief.Violation, "refusal supplies a new unresolved circumstance");
            Check.Equal(true, belief.ViolationCause > 0, "continuation has the actual reply as its cause");
            target.Personality.AddTrait(new TraitInstance("lawful")); target.Personality.AddTrait(new TraitInstance("kind"));
            world.Map.LocalTime.TurnCounter = 181;
            NpcIntentSupport.Turn(world, owner); NpcIntentSupport.Turn(world, target);
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "boundary_accepted"), "changed values lead to a different actual answer");
            Check.Equal(0, belief.Violation, "acceptance addresses the renewed boundary");
        });
    }
}
