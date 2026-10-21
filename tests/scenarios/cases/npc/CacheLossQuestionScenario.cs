using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheLossQuestionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-loss-question", () => TownScenarioFactory.Arena(4876,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "sociable");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            Actor friend = NpcIntentSupport.Actor(world, "friend", 1, 0, "kind");
            owner.Personality.Opinion(friend.PersonalityIdentity, friend.UnmodifiedName)
                .AdjustSocial(trust: 50);
            NpcIntentSupport.Player(world, 8, 2);
            var cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            Point at = new Point(2, 1);
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, at);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(6, 1));
            world.SetTile(4, 1, '#');
            world.Try(new ActionGetFromContainer(thief, world.Game, at));
            world.Map.PlaceActorAt(thief, new Point(7, 0));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionInspectStorage(owner, world.Game, cache)),
                "owner observes the empty cache");
            NpcFact loss = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_loss_discovered");
            Check.Equal(true, loss != null && loss.OtherId == Guid.Empty,
                "late discovery has no culprit");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "cache_loss_asked"),
                "discovery alone does not ask a question");
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(owner, "cache_loss_asked"); turn++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "cache_loss_asked"),
                "owner chooses to ask a trusted nearby person");
            Check.Equal(false, friend.Personality.Knowledge.Facts.Exists(f =>
                f.Kind == "cache_theft" && f.SubjectId == thief.PersonalityIdentity),
                "question does not disclose an unseen thief");
        });
    }
}
