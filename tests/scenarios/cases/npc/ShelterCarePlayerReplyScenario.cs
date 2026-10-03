using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ShelterCarePlayerReplyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/shelter-care-player-reply", () => TownScenarioFactory.Arena(4699,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor provider = NpcIntentSupport.Actor(world, "provider", 2, 1, "sociable", "honest", "humble");
            provider.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            world.Map.GetTileAt(5, 1).IsInside = true;
            provider.Personality.Knowledge.RememberPlace(new NpcKnownPlace(new Location(world.Map, new Point(5, 1)), "shelter", 0));
            player.HitPoints = 1;
            NpcEvents.Publish(world.Game, "requested_medicine", player, provider);
            NpcIntentSupport.Turn(world, provider);
            NpcReaction pending = NpcConversation.Pending(player, provider);
            Check.Equal(true, pending != null && pending.Kind == "shelter_care_offered",
                "player can answer the provider's actual shelter offer");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionPlayerTalk(player, world.Game, provider, true)),
                "player accepts through the normal talk action");
            Check.Equal(NpcServiceStatus.Accepted, provider.Personality.ServiceAgreements[0].Status,
                "player reply changes the provider's saved obligation");
            Check.Equal(NpcServiceStatus.Accepted, player.Personality.ServiceAgreements[0].Status,
                "player holds an independent copy of the agreement");
            Check.Equal(null, NpcConversation.Pending(player, provider), "reply clears the pending prompt");
        });
    }
}
