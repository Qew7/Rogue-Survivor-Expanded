using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheEncounterThreatScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-encounter-threat", () => TownScenarioFactory.Arena(4874,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "hotheaded", "rebellious");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position));
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "cache_threatened"),
                "witnessing alone does not produce a threat");
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(owner, "cache_threatened"); turn++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "cache_threatened"),
                "low patience and law produce a threat after an action");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "cache_return_requested"),
                "same encounter has one chosen response");
            Check.Equal(false, world.Game.Rules.AreEnemies(owner, thief),
                "speech alone does not start combat");
        });
    }
}
