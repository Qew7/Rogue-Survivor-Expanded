using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Personality;

static class FalseTheftAccusationCacheScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/false-theft-accusation-cache", () => TownScenarioFactory.Arena(9163,
            new string('.', 45), new string('.', 45), new string('.', 45)), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 44, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor liar = NpcIntentSupport.Actor(world, "liar", 3, 2, "deceptive");
            Actor enemy = NpcIntentSupport.Actor(world, "innocent", 43, 1);
            owner.Personality.Opinion(liar.PersonalityIdentity, liar.UnmodifiedName);
            liar.Personality.Knowledge.See(enemy, world.Map.LocalTime.TurnCounter);
            liar.Personality.Opinion(enemy.PersonalityIdentity, enemy.UnmodifiedName)
                .AdjustSocial(grievance: 30);
            Point at = new Point(2, 1);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, at);
            Check.Equal(true, world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true)),
                "owner opens the cache");
            ItemFood food = NpcIntentSupport.Food(world, owner, 1);
            world.Map.PlaceActorAt(owner, at);
            Check.Equal(true, world.Try(new ActionDropItem(owner, world.Game, food)),
                "owner stocks the cache");
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(liar, world.Game, at)),
                "deceptive NPC steals from the cache");
            NpcFact theft = liar.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            NpcFact lie = liar.Personality.Knowledge.Facts.Find(f => f.Kind == "theft_accusation");
            Check.Equal(true, theft != null && lie != null && lie.CauseId == theft.EventId &&
                lie.OtherId == enemy.PersonalityIdentity,
                "cache theft can prompt a false attribution to an enemy");
            Check.Equal(true, NpcConversation.ReportSentence(world.Game, liar, lie).Contains("cache") &&
                !NpcConversation.ReportSentence(world.Game, liar, lie).Contains("base"),
                "the claim describes a cache rather than a base");
        });
    }
}
