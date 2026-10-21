using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class PoliceDeadClaimantScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-dead-claimant", () => TownScenarioFactory.Arena(9152,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor member = NpcIntentSupport.Actor(world, "member", 2, 2);
            victim.AddFollower(member);
            Guid claimantGroup = victim.SocialGroup.Identity;
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            Actor cop = NpcIntentSupport.Actor(world, "cop", 6, 1, "lawful", "brave");
            cop.Faction = world.Game.GameFactions.ThePolice;
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            world.Map.AddXpdBase(new XpdBase(victim, new[] { at }));
            ItemFood item = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(item, at);
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game, at, item)),
                "item is stolen from a real group base");
            Check.Equal(claimantGroup, item.StolenFromGroupId, "theft records the claimant group");
            Check.Equal(victim.PersonalityIdentity, item.StolenFromLeaderId, "theft records the claimant leader");
            world.Place(thief, 5, 1);
            world.Game.DoGiveItemTo(thief, cop, item);
            Check.Equal(true, cop.Inventory.Items.Any(i => i.IsStolen && i.TheftEventId == item.TheftEventId),
                "officer physically holds identified stolen property");
            world.Game.KillActor(null, victim, "scenario", false);
            cop.Personality.Knowledge.See(member, world.Map.LocalTime.TurnCounter);
            for (int turn = 0; turn < 15 && !member.Inventory.Items.Any(i => i.StoryIdentity == item.StoryIdentity); turn++)
            { cop.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(cop); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, member.Inventory.Items.Any(i => i.StoryIdentity == item.StoryIdentity && !i.IsStolen),
                "officer returns goods to a living member when the original claimant died");
            Check.Equal(true, NpcIntentSupport.HasEvent(member, "police_goods_delivered"),
                "surviving group member observes the real return");
        });
    }
}
