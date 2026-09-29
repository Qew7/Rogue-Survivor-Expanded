using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ResourceItemNeedScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/resource-item-need", () => TownScenarioFactory.Arena(4822,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor claimant = NpcIntentSupport.Actor(world, "claimant", 1, 1);
            Actor holder = NpcIntentSupport.Actor(world, "holder", 2, 1, "kind");
            var bat = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT);
            holder.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = bat.Model.ID,
                ItemId = bat.StoryIdentity, Weight = 35 });
            Point place = new Point(1, 2); world.Map.DropItemAt(bat, place);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", claimant, holder,
                world.Map, claimant.Location.Position, 0) { Resource = "item", ResourcePlace = new Location(world.Map, place),
                ModelId = bat.Model.ID, ItemId = bat.StoryIdentity });
            NpcReaction refusal = holder.Personality.Reactions[0];
            Check.Equal("resource_refused", refusal.Kind, "healthy holder still needs their specific valued item");
            Check.Equal(true, world.Try(new ActionNpcReaction(holder, world.Game, refusal, claimant)), "refusal is actually spoken");

            holder.HitPoints = 1;
            var crowbar = new ItemMeleeWeapon(world.Game.GameItems.CROWBAR);
            Point other = new Point(2, 2); world.Map.DropItemAt(crowbar, other);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", claimant, holder,
                world.Map, claimant.Location.Position, 1) { Resource = "item", ResourcePlace = new Location(world.Map, other),
                ModelId = crowbar.Model.ID, ItemId = crowbar.StoryIdentity });
            NpcReaction concession = holder.Personality.Reactions[0];
            Check.Equal("resource_yielded", concession.Kind, "injury alone does not imply need for an unrelated item");
            world.Map.LocalTime.TurnCounter = 1;
            Check.Equal(true, world.Try(new ActionNpcReaction(holder, world.Game, concession, claimant)), "unrelated item can actually be yielded");
        });
    }
}
