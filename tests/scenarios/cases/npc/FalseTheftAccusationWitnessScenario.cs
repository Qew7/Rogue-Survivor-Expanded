using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class FalseTheftAccusationWitnessScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/false-theft-accusation-witness", () => TownScenarioFactory.Arena(9162,
            new string('.', 45), new string('.', 45), new string('.', 45)), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 44, 2);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1);
            Actor liar = NpcIntentSupport.Actor(world, "liar", 1, 2, "deceptive");
            Actor enemy = NpcIntentSupport.Actor(world, "innocent", 43, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 23, 1);
            liar.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            liar.Personality.Knowledge.See(enemy, world.Map.LocalTime.TurnCounter);
            liar.Personality.Opinion(enemy.PersonalityIdentity, enemy.UnmodifiedName)
                .AdjustSocial(grievance: 30);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, food)),
                "witness sees a real theft by someone else");
            NpcFact actual = liar.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            NpcFact lie = liar.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation");
            Check.Equal(true, actual != null && actual.SubjectId == thief.PersonalityIdentity &&
                lie != null && lie.SubjectId == liar.PersonalityIdentity &&
                lie.OtherId == enemy.PersonalityIdentity && lie.CauseId == actual.EventId,
                "deceptive eyewitness can frame a personal enemy without changing the real thief");
            Check.Equal(false, thief.Personality.Knowledge.Facts.Any(f => f.Kind == "theft_accusation"),
                "ordinary thief does not gain the witness's invented story");
            world.Place(liar, 22, 1);
            Check.Equal(lie, NpcConversation.Rumor(world.Game, liar, listener),
                "witness prefers telling the lie to a new listener");
            Check.Equal(true, world.Try(new ActionNpcTell(liar, world.Game, listener, lie)),
                "witness spreads the accusation through ordinary conversation");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Exists(f =>
                f.Kind == "theft_accusation" && f.Source == NpcKnowledgeSource.Told),
                "listener retains the claim as hearsay");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Any(f =>
                f.Kind == "base_theft" && f.EventId == actual.EventId),
                "liar does not reveal their contradictory eyewitness account in the same story");
        });
    }
}
