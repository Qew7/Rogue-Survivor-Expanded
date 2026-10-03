using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ConflictingTestimonyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/conflicting-testimony", () => TownScenarioFactory.Arena(4695,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 9, 2);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1, "deceptive", "sociable");
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "lawful");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2, "suspicious");
            Point at = new Point(2, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at); world.Game.DoTakeItem(thief, at, food);
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "base_theft"), "witness sees the real theft");
            NpcFact claim = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "claimed_permission");
            Check.Equal(true, claim != null, "deceptive thief forms a claim tied to the real act");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Exists(f => f.Kind == "base_theft" && f.EventId == claim.EventId),
                "eyewitness keeps a direct fact about that theft");
            Actor newcomer = NpcIntentSupport.Actor(world, "newcomer", 4, 1, "trusting");
            RelationshipRecord prior = witness.Personality.Person(thief.PersonalityIdentity);
            int before = prior == null ? 0 : prior.Grievance;
            Check.Equal(true, world.Try(new ActionNpcTell(thief, world.Game, newcomer, claim)), "thief actually tells the claim");
            Check.Equal(true, newcomer.Personality.Knowledge.Facts.Exists(f => f.Kind == "claimed_permission"), "newcomer hears a claim");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Exists(f => f.Kind == "claimed_permission"), "eyewitness hears the claim");
            Check.Equal(true, newcomer.Personality.Person(thief.PersonalityIdentity).Trust > 0,
                "a listener without contrary evidence provisionally trusts the claim");
            Check.Equal(true, witness.Personality.Person(thief.PersonalityIdentity).Grievance > before,
                "eyewitness distrusts the contradicting speaker");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "false_testimony_exposed"), "private contradiction is recorded");
            Check.Equal(true, witness.Personality.Memories.Any(m => m.Id == "false_testimony_exposed"),
                "contradiction becomes a personal memory");
            Actor third = NpcIntentSupport.Actor(world, "third listener", 5, 1, "trusting");
            NpcFact retold = newcomer.Personality.Knowledge.Facts.Find(f => f.Kind == "claimed_permission");
            Check.Equal(true, world.Try(new ActionNpcTell(newcomer, world.Game, third, retold)),
                "listener can pass the uncertain claim on as hearsay");
            Check.Equal(true, third.Personality.Knowledge.Facts.Exists(f => f.Kind == "claimed_permission" && f.Hops == 2),
                "retelling preserves the report's origin and hop count");
            Check.Equal(false, NpcConversation.CanTell(world.Game, witness,
                witness.Personality.Knowledge.Facts.Find(f => f.Kind == "claimed_permission")),
                "eyewitness does not repeat a refuted claim as fact");
            NpcFact refutation = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "false_testimony_exposed");
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, newcomer, refutation)),
                "eyewitness can report the contradiction");
            Check.Equal(true, newcomer.Personality.Person(thief.PersonalityIdentity).Grievance > 0,
                "reported refutation changes the liar's reputation");
            Check.Equal(false, world.Try(new ActionNpcTell(thief, world.Game, newcomer, claim)), "same claim cannot be farmed twice");
        });
    }
}
