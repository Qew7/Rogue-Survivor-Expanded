using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheSearchAnnouncementScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-search-announcement", () => TownScenarioFactory.Arena(4873,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1, "sociable");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1, "lawful");
            Actor owner = NpcIntentSupport.Actor(world, "owner", 6, 1);
            seeker.Personality.Opinion(listener.PersonalityIdentity, listener.UnmodifiedName)
                .AdjustSocial(trust: 40);
            listener.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName)
                .AdjustSocial(trust: 40);
            NpcIntentSupport.Player(world, 7, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(cache, new Point(5, 1));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(owner, NoteTopic.CacheSupplies, cache.Location, cache.Id, "food", 2, true);
            seeker.Inventory.AddAll(note);
            world.Try(new ActionUseItem(seeker, world.Game, note));
            Check.Equal(owner.PersonalityIdentity, seeker.Personality.Knowledge.Caches[0].ClaimantId,
                "signed paper identifies the source of a reported cache");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "cache_search_announced"),
                "private reading does not announce a search");
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(seeker, "cache_search_announced"); turn++)
            {
                seeker.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(seeker);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(seeker, "cache_search_announced"),
                "seeker chooses public speech about a known cache");
            NpcFact heard = listener.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_search_announced");
            Check.Equal(true, heard != null, "listener learns the announced intention");
            Check.Equal(owner.PersonalityIdentity, heard.ClaimantId,
                "announcement identifies a known claimant");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "intention does not become evidence of theft");
            world.Map.PlaceActorAt(listener, new Point(6, 0));
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(listener, "cache_owner_warned"); turn++)
            {
                listener.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(listener);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(listener, "cache_owner_warned"),
                "listener chooses to warn a known owner through speech");
        });
    }
}
