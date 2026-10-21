using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceRecoveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-recovery", () => TownScenarioFactory.Arena(9151,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "honest", "lawful");
            victim.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1, "lawful", "brave");
            cop.Faction = world.Game.GameFactions.ThePolice;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 0, 0, "lawful");
            Actor player = NpcIntentSupport.Player(world, 8, 2);
            Point baseAt = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { baseAt }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, baseAt);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, baseAt, food)),
                "suspect takes a real item from another person's base");
            NpcFact theft = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(true, theft != null && food.IsStolen && food.TheftEventId == theft.EventId,
                "witnessed theft and carried item share an event identity");
            Check.Equal(false, cop.Personality.Knowledge.Facts.Exists(f => f.EventId == theft.EventId),
                "wall prevents the officer from witnessing theft");

            world.Place(victim, 5, 0); world.Place(cop, 6, 0); world.Place(thief, 7, 0);
            Check.Equal(true, world.Try(new ActionNpcTell(victim, world.Game, cop, theft)),
                "victim tells the officer about the theft");
            NpcFact heard = cop.Personality.Knowledge.Facts.Find(f => f.EventId == theft.EventId);
            Check.Equal(NpcKnowledgeSource.Told, heard.Source,
                "officer starts from a report, not an invented eyewitness memory");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_theft_questioned"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_theft_questioned"),
                "officer questions the named suspect before demanding property");
            Check.Equal(false, player.Personality.Reactions.Any(r => r.Kind == "police_theft_questioned"),
                "a player who merely overhears questioning cannot answer for the suspect");
            Check.Equal(false, NpcIntentSupport.HasEvent(cop, "police_return_requested"),
                "hearsay alone does not trigger a demand");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(cop, "police_theft_admitted"); i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_theft_admitted"),
                "truthful suspect confirms the reported theft to the officer");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_return_requested"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_return_requested"),
                "officer requests the original item after admission");
            for (int i = 0; i < 8 && !NpcIntentSupport.HasEvent(cop, "police_return_accepted"); i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_return_accepted"),
                "suspect agrees through the social reply system");
            for (int i = 0; i < 12 && !cop.Inventory.Items.Any(it => it.IsStolen && it.TheftEventId == theft.EventId); i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, cop.Inventory.Items.Any(it => it.IsStolen && it.TheftEventId == theft.EventId),
                "suspect physically hands the original stolen item to the officer");
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_goods_surrendered"),
                "handover is a reportable story event");
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_escort_offered"),
                "officer offers a voluntary trip to return the property");
            for (int i = 0; i < 8 && thief.Leader != cop; i++)
            { thief.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(thief); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(cop, thief.Leader, "suspect agrees and temporarily follows the officer");
            long surrenderId = cop.Personality.Knowledge.Facts.Find(f => f.Kind == "police_goods_surrendered").EventId;
            string escortPath = Path.Combine(Path.GetTempPath(), "police-escort-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(escortPath, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(escortPath); loaded.ReconstructAuxiliaryFields();
                Actor savedCop = NpcIntentSupport.Find(loaded, cop.PersonalityIdentity);
                Actor savedThief = NpcIntentSupport.Find(loaded, thief.PersonalityIdentity);
                Check.Equal(savedCop, savedThief.Leader, "temporary escort survives save/load");
                Check.Equal(true, savedCop.Personality.Knowledge.Facts.Exists(f =>
                    f.Kind == "police_escort_started" && f.ItemId == food.StoryIdentity),
                    "saved escort retains the item and causal story identity");
            }
            finally
            {
                if (File.Exists(escortPath)) File.Delete(escortPath);
                if (File.Exists(escortPath + ".bak")) File.Delete(escortPath + ".bak");
            }
            int trustBefore = victim.Personality.Person(cop.PersonalityIdentity) == null ? 0 :
                victim.Personality.Person(cop.PersonalityIdentity).Trust;
            for (int i = 0; i < 12 && !victim.Inventory.Items.Any(it => it.StoryIdentity == food.StoryIdentity); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, victim.Inventory.Items.Any(it => it.StoryIdentity == food.StoryIdentity && !it.IsStolen),
                "officer returns the identified item to its living claimant");
            Check.Equal(null, thief.Leader, "escort ends when the property has been returned");
            Check.Equal(true, victim.Personality.Person(cop.PersonalityIdentity).Trust > trustBefore,
                "recipient responds to the actual return");
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "police_goods_delivered"),
                "return reaches recipient's remembered story");
            world.Place(listener, 5, 1);
            NpcFact returnReport = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "police_goods_delivered");
            Check.Equal(true, returnReport != null && returnReport.NamesSubject,
                "recipient can identify the officer who returned the item");
            Check.Equal(true, world.Try(new ActionNpcTell(victim, world.Game, listener, returnReport)),
                "recipient can tell a bystander about the real return");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == returnReport.EventId &&
                f.Source == NpcKnowledgeSource.Told), "bystander retains the return as hearsay");
            Check.Equal(true, listener.Personality.Person(cop.PersonalityIdentity) != null,
                "reported officer receives an attributed opinion");
            Check.Equal(true, listener.Personality.Person(cop.PersonalityIdentity).Trust > 0,
                "listener's social opinion of the officer improves after the report");

            string path = Path.Combine(Path.GetTempPath(), "police-recovery-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path); loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, victim.PersonalityIdentity);
                Check.Equal(true, saved.Inventory.Items.Any(it => it.StoryIdentity == food.StoryIdentity && !it.IsStolen),
                    "returned property and cleared stolen status survive save/load");
                Check.Equal(true, saved.Personality.Knowledge.Facts.Exists(f => f.Kind == "police_goods_delivered" && f.CauseId == surrenderId),
                    "the linked account survives save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
