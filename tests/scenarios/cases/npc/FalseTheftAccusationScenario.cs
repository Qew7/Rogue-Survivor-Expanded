using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class FalseTheftAccusationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/false-theft-accusation", () => TownScenarioFactory.Arena(9160,
            new string('.', 100), new string('.', 100), new string('.', 100)), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 99, 2);
            Actor thief = NpcIntentSupport.Actor(world, "liar", 1, 1, "deceptive");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 3, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2, "lawful");
            Actor noteWitness = NpcIntentSupport.Actor(world, "note witness", 0, 1, "lawful");
            Actor enemy = NpcIntentSupport.Actor(world, "innocent", 98, 1);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 31, 1, "trusting");
            witness.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            noteWitness.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            thief.Personality.Knowledge.See(enemy, world.Map.LocalTime.TurnCounter);
            thief.Personality.Opinion(enemy.PersonalityIdentity, enemy.UnmodifiedName)
                .AdjustSocial(grievance: 30);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)),
                "a real thief takes property before blaming an enemy");
            NpcFact theft = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            NpcFact accusation = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation");
            Check.Equal(true, theft != null && accusation != null &&
                accusation.CauseId == theft.EventId && accusation.OtherId == enemy.PersonalityIdentity,
                "deceptive thief frames a known enemy without altering the theft fact");
            Check.Equal(false, enemy.Personality.Knowledge.Facts.Any(f => f.Kind == "base_theft"),
                "the innocent person has no participant theft memory");
            world.Place(noteWitness, 75, 1);

            world.Place(thief, 30, 1);
            Check.Equal(accusation, NpcConversation.Rumor(world.Game, thief, relay),
                "autonomous rumor choice favors the lie the thief authored");
            Check.Equal(true, world.Try(new ActionNpcTell(thief, world.Game, relay, accusation)),
                "liar spreads the accusation as a claim");
            NpcFact heard = relay.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation");
            Check.Equal(true, heard != null && heard.Source == NpcKnowledgeSource.Told &&
                heard.SubjectId == thief.PersonalityIdentity && heard.OtherId == enemy.PersonalityIdentity,
                "listener retains accuser and accused separately");
            Check.Equal(true, relay.Personality.Person(enemy.PersonalityIdentity).Grievance > 0,
                "uncorroborated accusation affects a trusting listener's opinion");
            Check.Equal(false, relay.Personality.Knowledge.Facts.Any(f => f.Kind == "base_theft" &&
                f.SubjectId == enemy.PersonalityIdentity),
                "accusation never becomes an eyewitness theft fact");

            world.Place(relay, 2, 2);
            Check.Equal(true, world.Try(new ActionNpcTell(relay, world.Game, witness, heard)),
                "claim reaches someone who saw the real thief");
            NpcFact witnessedClaim = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation");
            Check.Equal(true, witnessedClaim != null, "eyewitness receives the claim");
            Check.Equal(theft.EventId, witnessedClaim.CauseId, "claim points to the witnessed theft");
            Check.Equal(true, witnessedClaim.NamesSubject, "claim names its author");
            Check.Equal(NpcKnowledgeSource.Witness, theft.Source, "eyewitness has direct evidence");
            Check.Equal(true, theft.NamesSubject, "eyewitness can name the actual thief");
            NpcFact refutation = witness.Personality.Knowledge.Facts.Find(f =>
                f.Kind == "false_testimony_exposed" && f.EventId == accusation.EventId);
            Check.Equal(true, refutation != null && refutation.SubjectId == thief.PersonalityIdentity,
                "eyewitness identifies the liar, not the innocent suspect");
            Check.Equal(false, NpcConversation.CanTell(world.Game, witness,
                witness.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation")),
                "eyewitness will not repeat a claim they disproved");
            Check.Equal(true, witness.Personality.Person(thief.PersonalityIdentity).Grievance > 0,
                "eyewitness reacts socially to the attempt to frame someone");

            world.Place(relay, 97, 1);
            Check.Equal(true, NpcConversation.CanTell(world.Game, relay, heard), "relay still permits the claim");
            Check.Equal(true, NpcConversation.EligibleFact(world.Game, relay, heard, world.Map.LocalTime.TurnCounter),
                "relay still holds a shareable claim");
            Check.Equal(true, NpcConversation.EligibleListener(world.Game, relay, enemy, heard),
                "innocent suspect is a new listener");
            Check.Equal(true, ActionNpcTell.CanAddress(world.Game, relay, enemy, heard),
                "relay can address the nearby suspect");
            Check.Equal(true, world.Try(new ActionNpcTell(relay, world.Game, enemy, heard)),
                "the innocent accused hears the relayed accusation");
            NpcFact denial = enemy.Personality.Knowledge.Facts.Find(f =>
                f.Kind == "theft_accusation_denied" && f.EventId == accusation.EventId);
            Check.Equal(true, denial != null && denial.SubjectId == enemy.PersonalityIdentity &&
                enemy.Personality.Person(thief.PersonalityIdentity).Grievance > 0,
                "innocent accused denies it and holds the liar responsible");
            Check.Equal(false, enemy.Personality.Knowledge.Facts.Any(f => f.Kind == "false_testimony_exposed"),
                "self-denial is not presented as independent eyewitness proof");
            int trustBeforeDenial = relay.Personality.Person(enemy.PersonalityIdentity).Trust;
            Check.Equal(true, world.Try(new ActionNpcTell(enemy, world.Game, relay, denial)),
                "the accused can tell the relayer their own denial");
            Check.Equal(true, relay.Personality.Knowledge.Facts.Exists(f =>
                f.Kind == "theft_accusation_denied" && f.Source == NpcKnowledgeSource.Told),
                "listener keeps the denial as a counterclaim");
            Check.Equal(true, relay.Personality.Person(enemy.PersonalityIdentity).Trust > trustBeforeDenial,
                "listener softens their view after hearing the denial");

            Actor third = NpcIntentSupport.Actor(world, "third", 50, 2, "lawful");
            world.Place(relay, 49, 1);
            Check.Equal(true, world.Try(new ActionNpcTell(relay, world.Game, third, heard)),
                "another listener first hears the uncorrected accusation");
            int grievanceBefore = third.Personality.Person(enemy.PersonalityIdentity).Grievance;
            world.Place(witness, 48, 2);
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, third, refutation)),
                "eyewitness can spread the correction");
            Check.Equal(true, third.Personality.Person(thief.PersonalityIdentity).Grievance > 0,
                "correction damages the original liar's reputation");
            Check.Equal(true, third.Personality.Person(enemy.PersonalityIdentity).Grievance < grievanceBefore,
                "correction reduces suspicion of the framed innocent person");

            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            thief.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionWriteNote(thief, world.Game, note,
                NoteTopic.Event, null, true, accusation)), "liar writes the same accusation");
            thief.Inventory.RemoveAllQuantity(note);
            noteWitness.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionUseItem(noteWitness, world.Game, note)),
                "a second eyewitness reads the accusation on paper");
            Check.Equal(true, noteWitness.Personality.Knowledge.Facts.Exists(f =>
                f.Kind == "false_testimony_exposed" && f.EventId == accusation.EventId),
                "reading paper also checks direct knowledge before affecting reputation");

            string path = Path.Combine(Path.GetTempPath(), "false-theft-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path); loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, enemy.PersonalityIdentity);
                Check.Equal(true, saved.Personality.Knowledge.Facts.Exists(f =>
                    f.Kind == "theft_accusation_denied" && f.EventId == accusation.EventId),
                    "denial and claim identity survive save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
