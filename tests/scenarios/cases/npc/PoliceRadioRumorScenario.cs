using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceRadioRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-radio-rumor", () => TownScenarioFactory.Arena(5834,
            "............", "............", "............"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor cop = NpcIntentSupport.Actor(world, "officer", 1, 1);
            cop.Faction = world.Game.GameFactions.ThePolice;
            Actor attacker = NpcIntentSupport.Actor(world, "Morgan", 2, 1);
            cop.Personality.Opinion(attacker.PersonalityIdentity, attacker.UnmodifiedName);
            Actor holder = NpcIntentSupport.Actor(world, "holder", 7, 1);
            Actor bystander = NpcIntentSupport.Actor(world, "bystander", 3, 1);
            Actor empty = NpcIntentSupport.Actor(world, "empty", 8, 1);
            Actor player = NpcIntentSupport.Player(world, 10, 1);
            holder.Inventory.AddAll(new ItemTracker(world.Game.GameItems.POLICE_RADIO));
            player.Inventory.AddAll(new ItemTracker(world.Game.GameItems.POLICE_RADIO));
            var drained = new ItemTracker(world.Game.GameItems.POLICE_RADIO) { Batteries = 0 };
            empty.Inventory.AddAll(drained);

            world.Game.DoMakeAggression(attacker, cop);
            NpcFact alert = holder.Personality.Knowledge.Facts.Single(f => f.Kind == "police_alert");
            Check.Equal(NpcKnowledgeSource.Told, alert.Source, "radio holder learns a retellable report");
            Check.Equal(attacker.PersonalityIdentity, alert.OtherId, "dispatch identifies the actual aggressor");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, holder, alert).Contains("armed and dangerous"),
                "ordinary rumor wording includes the police warning");
            Check.Equal(false, bystander.Personality.Knowledge.Facts.Any(f => f.Kind == "police_alert"),
                "nearby actor without a radio does not receive the private dispatch");
            Check.Equal(false, empty.Personality.Knowledge.Facts.Any(f => f.Kind == "police_alert"),
                "a discharged police radio receives nothing");
            Check.Equal(alert.EventId, player.Personality.Knowledge.Facts.Single(f => f.Kind == "police_alert").EventId,
                "player holding a police radio gets the same fact");
            Check.Equal(true, player.Personality.HeardJournal.Any(e => e.Kind == "heard_rumor" &&
                e.CauseId == alert.EventId && e.Text.Contains("armed and dangerous")),
                "the player's radio warning is kept with other heard rumors");

            world.Place(bystander, 7, 2);
            holder.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(holder, world.Game, bystander, alert)),
                "holder retells the dispatch through the ordinary rumor action");
            Check.Equal(alert.EventId, bystander.Personality.Knowledge.Facts.Single(f => f.Kind == "police_alert").EventId,
                "ordinary listener learns the original report");
            world.Game.DoMakeAggression(attacker, cop);
            Check.Equal(1, holder.Personality.Knowledge.Facts.Count(f => f.Kind == "police_alert"),
                "repeat aggression does not create another dispatch");
        });
    }
}
