using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceTimidOfficerScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-timid-officer", () => TownScenarioFactory.Arena(9156,
            "....#....", "....#....", "....#...."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "rebellious");
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1,
                "fearful", "cautious", "sociable");
            cop.Faction = world.Game.GameFactions.ThePolice;
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)),
                "a real theft creates the source of the suspect's account");
            NpcFact confession = thief.Personality.Knowledge.Facts.Find(f =>
                f.Kind == "base_theft" && f.Source == NpcKnowledgeSource.Participant);
            Check.Equal(true, confession != null, "suspect knows their own theft");
            world.Place(thief, 7, 1);
            Check.Equal(true, world.Try(new ActionNpcTell(thief, world.Game, cop, confession)),
                "suspect directly tells a hesitant officer about the theft");
            NpcFact heard = cop.Personality.Knowledge.Facts.Find(f => f.EventId == confession.EventId);
            Check.Equal(true, heard != null && heard.Source == NpcKnowledgeSource.Told &&
                heard.SourceId == thief.PersonalityIdentity,
                "officer retains who confessed instead of treating it as eyewitness evidence");
            for (int turn = 0; turn < 16 && !NpcIntentSupport.HasEvent(cop, "police_return_requested"); turn++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(cop, "police_return_requested"),
                "even a fearful officer usually chooses the long-term restitution goal after a direct confession");
            Check.Equal(false, NpcIntentSupport.HasEvent(cop, "police_theft_questioned"),
                "a direct confession does not need a second admission");
        });
    }
}
