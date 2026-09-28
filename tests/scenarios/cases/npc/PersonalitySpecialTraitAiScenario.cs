using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalitySpecialTraitAiScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-special-trait-ai", () => TownScenarioFactory.Arena(4576,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor speaker = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 0);
            speaker.Personality = new PersonalityState();
            speaker.Personality.AddTrait(new TraitInstance("suspicious"));
            Actor attacker = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheArmy, 0);
            Actor soldier = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheArmy, 1);
            Actor civilian = world.Game.GameActors.MaleCivilian.CreateNumberedName(
                world.Game.GameFactions.TheCivilians, 1);
            world.Place(speaker, 2, 1);
            world.Place(attacker, 2, 0);
            world.Place(soldier, 1, 1);
            world.Place(civilian, 3, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", speaker, attacker,
                world.Map, speaker.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(-23, PersonalitySystem.Attitude(speaker, soldier),
                "unresolved violence leaves an unfamiliar soldier above the trade rejection threshold");
            foreach (MemoryInstance memory in speaker.Personality.Memories)
                world.Map.LocalTime.TurnCounter = Math.Max(world.Map.LocalTime.TurnCounter, memory.ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, speaker.Personality.HasTrait("wary_army"), "violence develops distrust of Army");
            Check.Equal(-43, PersonalitySystem.Attitude(speaker, soldier),
                "acquired faction trait pushes other soldiers below the trade rejection threshold");
            world.Map.RemoveActor(attacker);
            speaker.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            soldier.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            civilian.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            speaker.FoodPoints = world.Game.Rules.ActorMaxFood(speaker);
            soldier.FoodPoints = world.Game.Rules.ActorMaxFood(soldier);
            civilian.FoodPoints = world.Game.Rules.ActorMaxFood(civilian);
            bool triedCivilian = false;
            for (int turn = 0; turn < 12; turn++)
            {
                speaker.Controller = new CivilianAI();
                speaker.ActionPoints = Rules.BASE_ACTION_COST;
                Check.Equal(true, world.NpcTurn(speaker), "civilian performs a legal AI turn");
                Check.Equal(false, Check.CallOn(typeof(BaseAI), speaker.Controller,
                    "IsActorTabooTrade", soldier), "faction trait prevents offering trade to an unfamiliar soldier");
                if ((bool)Check.CallOn(typeof(BaseAI), speaker.Controller,
                    "IsActorTabooTrade", civilian)) triedCivilian = true;
            }
            Check.Equal(true, triedCivilian, "civilian still offers trade to an unrelated faction");
            speaker.Personality.AddTrait(new TraitInstance("trusting"));
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", speaker, soldier,
                world.Map, speaker.Location.Position, world.Map.LocalTime.TurnCounter));
            foreach (MemoryInstance memory in speaker.Personality.Memories)
                world.Map.LocalTime.TurnCounter = Math.Max(world.Map.LocalTime.TurnCounter, memory.ResolveTurn);
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(false, speaker.Personality.HasTrait("friend_army"),
                "a conflicting faction trait cannot be awarded even with its prerequisite");
            RelationshipRecord relationship = speaker.Personality.Person(soldier.PersonalityIdentity);
            bool fallback = false;
            foreach (MemoryInstance memory in relationship.Memories)
                if (memory.Id == "aid_army") fallback = memory.OutcomeId == "skill:CHARISMATIC";
            Check.Equal(true, fallback, "conflicting trait outcome uses its related skill fallback");
        });
    }
}
