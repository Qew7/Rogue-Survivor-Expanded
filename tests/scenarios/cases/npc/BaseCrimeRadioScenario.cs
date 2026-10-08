using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class BaseCrimeRadioScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/base-crime-radio", () => TownScenarioFactory.Arena(4921,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor member = NpcIntentSupport.Actor(world, "member", 1, 0);
            Actor thief = NpcIntentSupport.Actor(world, "Vasily", 2, 1);
            thief.Faction = world.Game.GameFactions.TheBikers;
            Actor killer = NpcIntentSupport.Actor(world, "killer", 2, 0);
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 6, 0, "lawful", "kind");
            Actor rebel = NpcIntentSupport.Actor(world, "rebel", 7, 0, "rebellious", "cruel");
            Actor player = NpcIntentSupport.Player(world, 8, 0);
            Actor anonymousListener = NpcIntentSupport.Actor(world, "listener", 9, 0);
            owner.AddFollower(member);
            XpdBase claim = new XpdBase(owner, new[] { new Point(1, 0), new Point(1, 1),
                new Point(1, 2), new Point(2, 1) });
            claim.SetFoodRoom(new Rectangle(2, 1, 1, 1));
            world.Map.AddXpdBase(claim);
            witness.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);

            ItemFood first = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 };
            world.Map.DropItemAt(first, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), first);
            NpcFact anonymous = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            NpcFact loss = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "supplies_lost");
            Check.Equal(true, anonymous != null && loss != null, "real theft records break-in and lost supplies");
            Check.Equal(false, anonymous.NamesSubject, "unfamiliar thief is not identified by a witness");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, witness, anonymous).Contains("owner's base was robbed"),
                "unidentified theft is reported as a base robbery");
            Check.Equal(false, NpcConversation.ReportSentence(world.Game, witness, anonymous).Contains("biker"),
                "anonymous report does not leak the thief's faction");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, witness, loss).Contains("2 units of food"),
                "the outcome reports the actual supply damage");
            Check.Equal(anonymous.StoryId, loss.StoryId, "the robbery and losses form one story");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, anonymousListener, witness, anonymous),
                "anonymous report reaches another resident");
            Check.Equal(null, anonymousListener.Personality.Person(thief.PersonalityIdentity),
                "anonymous report does not damage personal reputation");
            Check.Equal(null, anonymousListener.Personality.Faction(thief.Faction.ID),
                "anonymous report does not blame a faction either");

            witness.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            ItemFood second = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 1 };
            world.Map.DropItemAt(second, new Point(2, 1));
            world.Game.DoTakeItem(thief, new Point(2, 1), second);
            NpcFact named = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft" && f.EventId != anonymous.EventId);
            Check.Equal(true, named.NamesSubject, "acquainted witness identifies the thief in a later theft");
            Check.Equal("Vasily", named.ReportSubject, "the named testimony retains the thief's identity");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, lawful, witness, named), "lawful resident hears named theft");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, rebel, witness, named), "rebellious resident hears named theft");
            Check.Equal(true, lawful.Personality.Person(thief.PersonalityIdentity).Feeling <
                rebel.Personality.Person(thief.PersonalityIdentity).Feeling,
                "listener traits change the thief's personal reputation");
            Check.Equal(false, player.Personality.Knowledge.Facts.Exists(f => f.EventId == named.EventId),
                "wall prevents the player from witnessing the theft");

            bool broadcast = false;
            for (int slot = 25; slot <= 42; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, slot);
                if (program.Facts == null || Array.Find(program.Facts, f => f.EventId == named.EventId) == null) continue;
                Check.Equal(true, program.Text.Contains("Vasily stole"), "crime station names the identified thief");
                Check.Call(world.Game, "BroadcastRadio", new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                    3, world.Map, player.Location.Position, null);
                Check.Equal(true, player.Personality.Person(thief.PersonalityIdentity).Feeling < 0,
                    "radio testimony changes the player's opinion of a named thief");
                broadcast = true;
                break;
            }
            Check.Equal(true, broadcast, "crime station eventually airs the witnessed theft");

            Actor newListener = NpcIntentSupport.Actor(world, "new listener", 11, 0);
            world.Place(witness, 10, 0);
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, newListener, loss)),
                "witness can tell the linked base incident directly");
            Check.Equal(true, newListener.Personality.Knowledge.Facts.Exists(f => f.EventId == anonymous.EventId) &&
                newListener.Personality.Knowledge.Facts.Exists(f => f.EventId == loss.EventId) &&
                newListener.Personality.Knowledge.Facts.Exists(f => f.EventId == named.EventId),
                "direct conversation delivers the robbery, damage, and identified continuation together");
            world.Place(witness, 0, 1);
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            world.Game.KillActor(killer, member, "scenario", false);
            NpcFact raid = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "base_raid");
            NpcFact murder = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "murder");
            Check.Equal(true, raid != null, "outsider killing a member inside its base records a raid");
            Check.Equal(murder.StoryId, raid.StoryId, "murder and base raid share one story");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, witness, raid).Contains("base was raided"),
                "witness can report a fatal base raid");
            Check.Equal(true, NpcKnowledgeSystem.Hear(world.Game, lawful, witness, raid), "raid news can reach radio sources");
            bool raidBroadcast = false;
            for (int slot = 48; slot <= 71; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, slot);
                if (program.Facts != null && Array.Find(program.Facts, f => f.EventId == raid.EventId) != null)
                { raidBroadcast = program.Text.Contains("was raided") && program.Text.Contains("was killed") &&
                    !program.Text.Contains("base was raided"); break; }
            }
            Check.Equal(true, raidBroadcast, "crime station also airs the fatal raid");

            world.Place(thief, 5, 1);
            Actor insider = NpcIntentSupport.Actor(world, "insider", 2, 1);
            owner.AddFollower(insider);
            world.Game.KillActor(owner, insider, "scenario", false);
            NpcFact insideMurder = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "murder" && f.SubjectId == insider.PersonalityIdentity);
            Check.Equal(true, insideMurder != null && insideMurder.Resource == "base",
                "a member killed by their own group is still reported at the base");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, witness, insideMurder).Contains("at their group's base"),
                "the killing report names the base context without calling it a raid");
            Check.Equal(1, witness.Personality.Knowledge.Facts.FindAll(f => f.Kind == "base_raid").Count,
                "a killing by the base owner is not reported as an outside raid");
            Actor accident = NpcIntentSupport.Actor(world, "accident", 1, 2);
            owner.AddFollower(accident);
            int raids = witness.Personality.Knowledge.Facts.FindAll(f => f.Kind == "base_raid").Count;
            world.Game.KillActor(null, accident, "scenario", false);
            Check.Equal(raids, witness.Personality.Knowledge.Facts.FindAll(f => f.Kind == "base_raid").Count,
                "death without an attacker is not called a raid");
        });
    }
}
