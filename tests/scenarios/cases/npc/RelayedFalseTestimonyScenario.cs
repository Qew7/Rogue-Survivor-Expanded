using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RelayedFalseTestimonyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/relayed-false-testimony", () => TownScenarioFactory.Arena(4862,
            "..............................", "..............................", ".............................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 29, 2);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1, "deceptive", "sociable");
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "lawful");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2, "suspicious");
            Point at = new Point(2, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            world.Game.DoTakeItem(thief, at, food);
            NpcFact claim = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "claimed_permission");
            Check.Equal(true, claim != null && NpcIntentSupport.HasEvent(witness, "base_theft"),
                "thief invents a permission claim about a witnessed theft");

            world.Place(witness, 20, 1);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 4, 1, "trusting");
            Check.Equal(true, world.Try(new ActionNpcTell(thief, world.Game, relay, claim)),
                "thief tells a distant listener");
            Check.Equal(false, witness.Personality.Knowledge.Facts.Exists(f => f.Kind == "claimed_permission"),
                "eyewitness has not heard the original claim");
            world.Place(relay, 17, 1);
            NpcFact retold = relay.Personality.Knowledge.Facts.Find(f => f.Kind == "claimed_permission");
            Check.Equal(true, world.Try(new ActionNpcTell(relay, world.Game, witness, retold)),
                "listener relays the claim to the eyewitness");

            NpcFact refutation = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "false_testimony_exposed");
            Check.Equal(true, refutation != null && refutation.SubjectId == thief.PersonalityIdentity,
                "refutation identifies the original claimant");
            Check.Equal(true, witness.Personality.Memories.Any(m => m.Id == "false_testimony_exposed" &&
                m.SubjectId == thief.PersonalityIdentity), "memory identifies the claimant");
            RelationshipRecord relayerOpinion = witness.Personality.Person(relay.PersonalityIdentity);
            Check.Equal(true, relayerOpinion == null || relayerOpinion.Grievance == 0,
                "eyewitness does not blame the relayer for the false claim");
            Actor third = NpcIntentSupport.Actor(world, "third", 22, 1, "trusting");
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, third, refutation)),
                "eyewitness reports the contradiction");
            Check.Equal(true, third.Personality.Person(thief.PersonalityIdentity).Grievance > 0,
                "refutation damages the claimant's reputation downstream");
            RelationshipRecord thirdRelayerOpinion = third.Personality.Person(relay.PersonalityIdentity);
            Check.Equal(true, thirdRelayerOpinion == null || thirdRelayerOpinion.Grievance == 0,
                "downstream listener does not blame the relayer");
        });
    }
}
