using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityFactionExperiencesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-faction-experiences", () => TownScenarioFactory.Arena(4571,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            foreach (PersonalityWorldContent.FactionSource faction in PersonalityWorldContent.Factions)
            {
                if (faction.Id == GameFactions.IDs.TheCivilians) continue;
                bool canHelp = faction.Id != GameFactions.IDs.TheUndeads && faction.Id != GameFactions.IDs.TheFerals;
                for (int phase = 0; phase < (canHelp ? 2 : 1); phase++)
                {
                    bool help = phase == 1;
                    Actor observer = new Actor(world.Game.GameActors.MaleCivilian,
                        world.Game.GameFactions.TheCivilians, "observer", true, false, 0);
                    observer.Personality = new PersonalityState();
                    observer.Personality.AddTrait(new TraitInstance(help ? "trusting" : "suspicious"));
                    Actor source = new Actor(faction.Id == GameFactions.IDs.TheUndeads ? world.Game.GameActors.Zombie :
                        faction.Id == GameFactions.IDs.TheFerals ? world.Game.GameActors.FeralDog : world.Game.GameActors.MaleCivilian,
                        world.Game.GameFactions[faction.Id], "source", true, false, 0);
                    Actor stranger = new Actor(world.Game.GameActors.MaleCivilian,
                        source.Faction, "stranger", true, false, 0);
                    world.Place(observer, 1, 1);
                    world.Place(source, 2, 1);
                    world.Place(stranger, 3, 1);
                    if (help)
                    {
                        source.Controller = new PlayerController();
                        world.SetPlayer(source);
                        observer.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
                        ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
                        source.Inventory.AddAll(food);
                        world.Game.DoGiveItemTo(source, observer, food);
                        Check.Equal(true, observer.Inventory.Contains(food), "real aid reaches recipient");
                    }
                    else PersonalitySystem.Report(world.Game, new SignificantEvent("attack", observer, source,
                        world.Map, observer.Location.Position, world.Map.LocalTime.TurnCounter));
                    string id = (help ? "aid_" : "violence_") + faction.Key;
                    bool remembered = false;
                    foreach (MemoryInstance memory in observer.Personality.Memories)
                        if (memory.Id == id) remembered = true;
                    Check.Equal(true, remembered, faction.Name + ": faction-specific memory is generated");
                    int before = PersonalitySystem.Attitude(observer, stranger);
                    foreach (MemoryInstance memory in observer.Personality.Memories)
                        world.Map.LocalTime.TurnCounter = Math.Max(world.Map.LocalTime.TurnCounter, memory.ResolveTurn);
                    PersonalitySystem.ResolveDue(world.Game, world.Map);
                    Check.Equal(true, observer.Personality.HasTrait((help ? "friend_" : "wary_") + faction.Key),
                        faction.Name + ": appropriate base trait permits the special outcome");
                    Check.Equal(before + (help ? 15 : -20), PersonalitySystem.Attitude(observer, stranger),
                        "acquired trait changes attitude toward another member of this faction");
                    Check.Equal(0, PersonalitySystem.Attitude(observer, observer), "trait does not create a self relation");
                    Actor neutral = new Actor(world.Game.GameActors.MaleCivilian,
                        world.Game.GameFactions.TheCivilians, "neutral", true, false, 0);
                    Check.Equal(0, PersonalitySystem.Attitude(observer, neutral),
                        "faction experience does not change attitude toward an unrelated faction");
                    GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
                    disabled.NpcPersonalitiesEnabled = false;
                    Session.Get.GamePreset = disabled;
                    Check.Equal(0, PersonalitySystem.Attitude(observer, stranger), "disabled preset suppresses special faction bias");
                    Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
                    world.Map.RemoveActor(observer);
                    world.Map.RemoveActor(source);
                    world.Map.RemoveActor(stranger);
                }
            }
        });
    }
}
