using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberyFlightScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-flight-rumor", () => TownScenarioFactory.Arena(9764,
            "..............................", "..............................", ".............................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor robber = NpcIntentSupport.Actor(world, "robber", 2, 1, "cruel", "selfish", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1, "independent", "impulsive");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "lawful", "kind");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 24, 1, "lawful", "kind");
            NpcIntentSupport.Player(world, 29, 2);
            ItemFood item = NpcIntentSupport.Food(world, victim, 1);
            witness.Personality.Opinion(robber.PersonalityIdentity, robber.UnmodifiedName);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("robbery_demanded", robber, victim,
                world.Map, robber.Location.Position, 0)
                { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
            victim.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(victim);
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_fled"),
                "victim with moderate resolve chooses to flee while retaining the item");
            Check.Equal(true, victim.Inventory.Contains(item), "flight does not surrender the item");
            for (int i = 0; i < 5 && world.Game.Rules.GridDistance(victim.Location.Position, robber.Location.Position) <= 1; i++)
            { victim.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(victim); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, world.Game.Rules.GridDistance(victim.Location.Position, robber.Location.Position) > 1,
                "fleeing victim physically moves away from the robber");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_fled" &&
                f.SubjectId == victim.PersonalityIdentity && f.OtherId == robber.PersonalityIdentity),
                "witness remembers flight as a separate response");
            NpcFact fact = witness.Personality.Knowledge.Facts.Single(f => f.Kind == "robbery_fled");
            world.Map.RemoveActor(witness); world.Place(witness, 23, 1);
            witness.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, listener, fact)),
                "witness shares the flight rumor through normal conversation");
            Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_fled" &&
                f.Source == NpcKnowledgeSource.Told), "listener keeps flight as hearsay");
            Check.Equal(true, listener.Personality.Person(robber.PersonalityIdentity) != null &&
                listener.Personality.Person(robber.PersonalityIdentity).Grievance > 0,
                "lawful listener judges the robber after hearing how the victim fled");
        });
    }
}
