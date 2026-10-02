using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class GroupSupplyRuleScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-supply-rule", () => TownScenarioFactory.Arena(4693,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 9, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 2, 1, "kind", "generous");
            Actor member = NpcIntentSupport.Actor(world, "member", 3, 1, "lawful", "loyal");
            Actor sleeper = NpcIntentSupport.Actor(world, "sleeper", 4, 1, "kind", "generous");
            sleeper.IsSleeping = true;
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1, "rebellious");
            leader.AddFollower(member); leader.AddFollower(sleeper);
            Point at = new Point(2, 2);
            world.Map.AddXpdBase(new XpdBase(leader, new[] { at }));
            for (int i = 0; i < 2; i++)
            {
                var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
                world.Map.DropItemAt(food, at);
                world.Game.DoTakeItem(thief, at, food);
            }
            Check.Equal(true, leader.SocialGroup.SupplyRule >= 30, "witnessed repeated theft creates a restrictive rule");
            Check.Equal(true, NpcIntentSupport.HasEvent(member, "group_supply_rule_strict"), "members witness the announced rule");
            Check.Equal(true, member.Personality.Memories.Count > 0, "the rule creates a group memory");
            Check.Equal(0, sleeper.Personality.KnownSupplyRule(leader.SocialGroup.Identity),
                "sleeping member does not learn a rule just because the group shares state");
            sleeper.IsSleeping = false;
            NpcFact rule = leader.Personality.Knowledge.Facts.Find(f => f.Kind == "group_supply_rule_strict");
            Check.Equal(true, world.Try(new ActionNpcTell(leader, world.Game, sleeper, rule)),
                "leader can actually tell an absent member the new rule");
            Check.Equal(true, sleeper.Personality.KnownSupplyRule(leader.SocialGroup.Identity) >= 30,
                "heard rule becomes the member's own knowledge");
            world.Place(thief, 0, 0);
            Actor requester = NpcIntentSupport.Actor(world, "requester", 1, 1, "solitary", "lawful");
            requester.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }, at);
            Location cache = new Location(world.Map, at);
            world.Game.DoSay(requester, leader, "Can you leave those supplies for me?",
                RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST);
            PersonalitySystem.Report(world.Game, new SignificantEvent("resource_contested", requester, leader,
                world.Map, requester.Location.Position, 0) { ResourcePlace = cache, Resource = "food" });
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "resource_contested"), "leader hears a real request for supplies");
            NpcReaction reply = leader.Personality.Reactions.Find(r => r.Kind == "resource_refused" && r.TargetId == requester.PersonalityIdentity);
            Check.Equal(true, reply != null, "group rule changes the leader's chosen reply");
            leader.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcReaction(leader, world.Game, reply, requester)), "leader actually speaks the refusal");
            Check.Equal(true, NpcIntentSupport.HasEvent(requester, "resource_refused"),
                "learned group rule overrides a generous leader's willingness to yield");
            Actor distant = NpcIntentSupport.Actor(world, "sleeping owner", 7, 1, "lawful");
            distant.IsSleeping = true;
            Point remote = new Point(7, 2);
            world.Map.AddXpdBase(new XpdBase(distant, new[] { remote }));
            var remoteFood = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(remoteFood, remote);
            world.Game.DoTakeItem(thief, remote, remoteFood);
            Check.Equal(0, distant.SocialGroup == null ? 0 : distant.SocialGroup.SupplyRule,
                "an unwitnessed theft does not change a group's rule");
        });
    }
}
