using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;

static class PersonalityCannibalScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-cannibal", () => TownScenarioFactory.Arena(4517,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor eater = SkillScenario.Actor(world);
            eater.Personality = new PersonalityState();
            eater.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            Actor dead = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "dead", false, false, 0);
            Corpse corpse = new Corpse(dead, 100, 100, 0, 0, 1);
            world.Map.AddCorpseAt(corpse, eater.Location.Position);

            Check.Equal(false, world.Game.Rules.CanActorEatCorpse(eater, corpse),
                "ordinary hungry human cannot eat a corpse");
            eater.Personality.AddTrait(new TraitInstance("pragmatic"));
            eater.Personality.AddTrait(new TraitInstance("cannibal"));
            Check.Equal(true, world.Game.Rules.CanActorEatCorpse(eater, corpse),
                "cannibal trait permits eating while hungry");
            int food = eater.FoodPoints;
            Check.Equal(true, world.Try(new ActionEatCorpse(eater, world.Game, corpse)),
                "cannibal performs real corpse-eating action");
            Check.Equal(true, eater.FoodPoints > food, "action provides food");

            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            eater.FoodPoints = disabled.HungerPoints - 1;
            Check.Equal(false, world.Game.Rules.CanActorEatCorpse(eater, corpse),
                "disabling system removes cannibal permission");
        });
    }
}
