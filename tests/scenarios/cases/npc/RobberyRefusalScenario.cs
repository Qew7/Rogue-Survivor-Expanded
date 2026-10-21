using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberyRefusalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-refusal", () => TownScenarioFactory.Arena(9765,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor robber = NpcIntentSupport.Actor(world, "robber", 2, 1, "cruel", "selfish", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1, "brave", "independent", "impulsive");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "lawful");
            NpcIntentSupport.Player(world, 6, 2);
            ItemFood item = NpcIntentSupport.Food(world, victim, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("robbery_demanded", robber, victim,
                world.Map, robber.Location.Position, 0)
                { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
            victim.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(victim);
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_refused"),
                "brave victim refuses without striking first");
            Check.Equal(true, victim.Inventory.Contains(item), "refusal keeps possession of the item");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_refused" &&
                f.SubjectId == victim.PersonalityIdentity && f.OtherId == robber.PersonalityIdentity),
                "witness records the refusal for later retelling");
        });
    }
}
