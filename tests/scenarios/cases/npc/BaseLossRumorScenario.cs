using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class BaseLossRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/base-loss-rumor", () => TownScenarioFactory.Arena(4907,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_XPD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor owner = NpcIntentSupport.Actor(world, "Peter Steel", 1, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            witness.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(1, 1) }));
            Check.Call(world.Game, "ReleaseGroupBases", new[] { typeof(Actor), typeof(XpdBase) }, owner, null);
            Check.Equal(null, world.Map.XpdBaseAt(new Point(1, 1)), "the base is actually released");
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "base_loss");
            Check.Equal(true, fact != null, "witness retains the real base loss");
            Check.Equal("Peter Steel", fact.ReportSubject, "known owner is named");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == fact.EventId),
                "remote listener did not witness the base loss");

            world.Place(witness, 4, 0); world.Place(listener, 5, 0); world.Place(player, 6, 0);
            var tell = new ActionNpcTell(witness, world.Game, listener, fact);
            Check.Equal(true, tell.IsLegal(), "witness can share base news"); tell.Perform();
            Check.Equal(NpcKnowledgeSource.Told, listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId).Source,
                "listener learns the base loss as hearsay");
            Check.Equal(true, player.Personality.HeardJournal[0].Text.Contains("Peter Steel lost a base"),
                "player hears the base owner's name");
        });
    }
}
