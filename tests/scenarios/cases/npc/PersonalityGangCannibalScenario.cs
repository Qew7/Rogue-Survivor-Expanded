using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityGangCannibalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-gang-cannibal", () => TownScenarioFactory.Arena(4540,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor biker = world.Game.GameActors.BikerMan.CreateNumberedName(
                world.Game.GameFactions.TheBikers, 0);
            biker.Personality = new PersonalityState();
            biker.Personality.AddTrait(new TraitInstance("pragmatic"));
            biker.Personality.AddTrait(new TraitInstance("cannibal"));
            biker.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            world.Place(biker, 2, 1);
            Actor dead = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "dead", false, false, 0);
            Corpse corpse = new Corpse(dead, 100, 100, 0, 0, 1);
            world.Map.AddCorpseAt(corpse, biker.Location.Position);

            int before = biker.FoodPoints;
            Check.Equal(true, world.NpcTurn(biker), "GangAI makes a legal eating decision");
            Check.Equal(true, biker.FoodPoints > before,
                "hungry cannibal gang member eats a sensed corpse");

            world.Map.RemoveCorpse(corpse);
            biker.FoodPoints = before;
            biker.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.NpcTurn(biker), "GangAI acts without a corpse");
            Check.Equal(before, biker.FoodPoints,
                "no corpse means no corpse-eating effect");
        });
    }
}
