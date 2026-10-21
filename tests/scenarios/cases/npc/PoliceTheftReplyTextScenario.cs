using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceTheftReplyTextScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-theft-reply-text", () => TownScenarioFactory.Arena(9155,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "honest", "lawful");
            victim.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1, "lawful", "brave");
            cop.Faction = world.Game.GameFactions.ThePolice;
            Actor innocent = NpcIntentSupport.Actor(world, "innocent", 7, 1);
            Actor player = NpcIntentSupport.Player(world, 8, 2);
            Point baseAt = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { baseAt }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, baseAt);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, baseAt, food)), "real theft succeeds");
            NpcFact theft = victim.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(true, theft != null, "victim identifies the theft");

            world.Place(victim, 5, 0); world.Place(cop, 6, 0); world.Place(thief, 7, 0);
            Check.Equal(true, world.Try(new ActionNpcTell(victim, world.Game, cop, theft)),
                "victim reports the theft to the officer");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(cop, "police_theft_questioned"); i++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_theft_questioned"),
                "officer questions the actual suspect");
            NpcReaction admission = thief.Personality.Reactions.Find(r => r.Kind == "police_theft_admitted");
            Check.Equal(true, admission != null, "honest thief queues an admission");
            Check.Equal("I took it.", admission.Text, "admission uses admission speech");
            Check.Equal(true, world.Try(new ActionNpcReaction(thief, world.Game, admission, cop)),
                "thief gives the queued answer");
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_theft_admitted"),
                "the spoken admission reports an admission event");
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.Text.Contains("I took it.")),
                "nearby player hears the admission");

            PersonalitySystem.Report(world.Game, new SignificantEvent("police_theft_questioned", cop, innocent,
                world.Map, cop.Location.Position, world.Map.LocalTime.TurnCounter,
                causeId: theft.EventId, storyId: theft.StoryId));
            NpcReaction denial = innocent.Personality.Reactions.Find(r => r.Kind == "police_theft_denied");
            Check.Equal(true, denial != null, "person without participant evidence queues a denial");
            Check.Equal("I did not take it.", denial.Text, "denial uses denial speech");
            Check.Equal(true, world.Try(new ActionNpcReaction(innocent, world.Game, denial, cop)),
                "innocent person gives the queued answer");
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_theft_denied"),
                "the spoken denial reports a denial event");
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.Text.Contains("I did not take it.")),
                "nearby player hears the denial");
        });
    }
}
