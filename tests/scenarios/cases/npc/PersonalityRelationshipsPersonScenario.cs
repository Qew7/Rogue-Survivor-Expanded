using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityRelationshipsPersonScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-relationships-person", () => TownScenarioFactory.Arena(4551,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor helper = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            helper.Controller = new PlayerController();
            Actor namesake = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor recipient = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "recipient", false, false, 0);
            recipient.Personality = new PersonalityState();
            world.Place(helper, 1, 1);
            world.Place(recipient, 2, 1);
            world.Place(namesake, 3, 1);
            world.SetPlayer(helper);
            int trustBeforeHelp = world.Game.Rules.ActorTrustIncrease(helper, recipient);

            recipient.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, helper.Inventory.AddAll(food), "helper carries food");
            world.Game.DoGiveItemTo(helper, recipient, food);
            Check.Equal(true, recipient.Inventory.Contains(food), "real gift reaches recipient");
            Check.Equal(1, recipient.Personality.Memories.Count, "needed gift starts a memory");
            RelationshipRecord relationship = recipient.Personality.Person(helper.PersonalityIdentity);
            Check.Equal(true, relationship != null, "memory belongs to its helper");
            Check.Equal(1, relationship.Memories.Count, "relationship retains the episode");
            Check.Equal(30, PersonalitySystem.Attitude(recipient, helper), "help improves personal attitude");
            Check.Equal(trustBeforeHelp + 3, world.Game.Rules.ActorTrustIncrease(helper, recipient),
                "personal help makes trust in this leader grow faster");
            Check.Equal(0, PersonalitySystem.Attitude(recipient, namesake),
                "another actor with the same name inherits no attitude");

            ItemFood secondFood = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, namesake.Inventory.AddAll(secondFood), "namesake carries separate food");
            world.Game.DoGiveItemTo(namesake, recipient, secondFood);
            Check.Equal(2, recipient.Personality.Memories.Count,
                "two helpers with one name create separate pending memories");
            Check.Equal(30, PersonalitySystem.Attitude(recipient, namesake),
                "second helper earns a separate personal impression");
            Check.Equal(1, recipient.Personality.Person(namesake.PersonalityIdentity).Memories.Count,
                "second helper has their own episode");

            string description = String.Join(" ", (string[])Check.Call(world.Game, "DescribeActor",
                new[] { typeof(Actor) }, recipient));
            Check.Equal(false, description.Contains("Memory:"),
                "an outsider cannot inspect the NPC's private memories");

            world.Map.LocalTime.TurnCounter = Math.Max(recipient.Personality.Memories[0].ResolveTurn,
                recipient.Personality.Memories[1].ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(0, recipient.Personality.Memories.Count, "resolved memory leaves pending queue");
            Check.Equal(1, relationship.Memories.Count, "resolved episode stays with the helper");
            Check.Equal(world.Map.LocalTime.TurnCounter, relationship.Memories[0].ResolvedTurn,
                "relationship records when the episode resolved");
            Check.Equal("skill:MEDIC", relationship.Memories[0].OutcomeId,
                "relationship records the outcome");
            Check.Equal(30, PersonalitySystem.Attitude(recipient, helper),
                "personal attitude survives memory resolution");
        });
    }
}
