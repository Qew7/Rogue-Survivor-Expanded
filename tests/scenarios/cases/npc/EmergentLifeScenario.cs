using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class EmergentLifeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/emergent-life", () => TownScenarioFactory.Arena(4675, ".........", ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 3);
            Actor recipient = NpcIntentSupport.Actor(world, "recipient", 2, 1, "sociable", "trusting");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind", "honest");
            Actor competitor = NpcIntentSupport.Actor(world, "competitor", 6, 1, "solitary", "rebellious");
            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1; competitor.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, recipient); NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, helper.Personality.Commitments.Count, "starting circumstances create a spoken obligation");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 4 }, new Point(2, 2));
            foreach (int turn in new[] { 1, 2, 3 })
            { world.Map.LocalTime.TurnCounter = turn; NpcIntentSupport.Turn(world, helper); }
            Actor[] actors = { recipient, helper, competitor };
            for (int turn = 4; turn <= 360; turn++)
            {
                world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter = turn;
                NpcIntentSystem.AdvanceClock(world.Game, world.Map);
                foreach (Actor actor in actors)
                {
                    actor.ActionPoints = Rules.BASE_ACTION_COST;
                    for (int attempt = 0; actor.ActionPoints > 0 && attempt < 10; attempt++)
                        Check.Equal(true, world.NpcTurn(actor), "production AI always performs a legal action in the evolving situation");
                    Check.Equal(true, actor.ActionPoints <= 0, "free preparation cannot produce an infinite action loop");
                    int active = 0; foreach (NpcIntent goal in actor.Personality.Intents) if (!goal.Finished) active++;
                    Check.Equal(true, active <= 4 && actor.Personality.Intents.Count <= 12, "goals stay within saved budgets");
                    Check.Equal(true, actor.Personality.Commitments.Count <= 16 && actor.Personality.Disputes.Count <= 16, "social state remains bounded");
                }
                int units = 0; foreach (Actor actor in actors) units += NpcIntentSupport.FoodUnits(actor);
                for (int x = 0; x < world.Map.Width; x++) for (int y = 0; y < world.Map.Height; y++)
                { Inventory ground = world.Map.GetItemsAt(new Point(x, y)); if (ground != null) foreach (Item item in ground.Items) if (item is ItemFood) units += item.Quantity; }
                Check.Equal(true, units <= 4, "interaction, replanning and repeated observation never manufacture resources");
            }
            Check.Equal(NpcCommitmentStatus.Kept, helper.Personality.Commitments[0].Status, "the actual outcome survives later unrelated decisions");
            Check.Equal(true, recipient.Personality.Person(helper.PersonalityIdentity).Memories.Count > 0, "the evolving life retains its personal history");
            foreach (NpcStory story in Session.Get.NpcDirector.Stories)
                Check.Equal(false, story.Parents.Contains(story.Id), "causal episodes never become their own parent");
        });
    }
}
