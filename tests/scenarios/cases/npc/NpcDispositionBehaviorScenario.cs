using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcDispositionBehaviorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/disposition-behavior", () => TownScenarioFactory.Arena(8161,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor other = NpcIntentSupport.Actor(world, "other", 2, 1);
            owner.Personality = new PersonalityState();
            int civilianLife = PersonalitySystem.Disposition(owner, NpcDispositionAxis.HumanLife, world.Game.NpcContent);
            owner.Faction = world.Game.GameFactions.ThePsychopaths;
            Check.Equal(true, PersonalitySystem.Disposition(owner, NpcDispositionAxis.HumanLife, world.Game.NpcContent) < civilianLife,
                "faction sets the starting value of human life");
            owner.Faction = world.Game.GameFactions.TheCivilians;
            owner.Personality.AddTrait(new TraitInstance("kind"));
            Check.Equal(true, PersonalitySystem.Disposition(owner, NpcDispositionAxis.HumanLife, world.Game.NpcContent) > civilianLife,
                "a current trait changes the value without rewriting experience");
            owner.Personality.RemoveTrait("kind");
            Check.Equal(civilianLife, PersonalitySystem.Disposition(owner, NpcDispositionAxis.HumanLife, world.Game.NpcContent),
                "losing a trait immediately removes its contribution");

            int care = NpcValues.Importance(world.Game.NpcContent, owner, NpcGoalValue.Care, other.PersonalityIdentity);
            owner.Personality.ChangeExperience(NpcDispositionAxis.CareCircle, 40);
            Check.Equal(true, NpcValues.Importance(world.Game.NpcContent, owner, NpcGoalValue.Care, other.PersonalityIdentity) > care,
                "a wider care circle raises the actual food-aid goal for a stranger");
            int confidence = NpcKnowledgeSystem.ReportConfidence(owner, other, new NpcFact { Confidence = 80 });
            owner.Personality.ChangeExperience(NpcDispositionAxis.MessageTrust, 45);
            Check.Equal(true, NpcKnowledgeSystem.ReportConfidence(owner, other, new NpcFact { Confidence = 80 }) > confidence,
                "message trust changes hearsay confidence");
            int reserve = world.Game.NpcContent.Value("FoodReserve").Importance(
                new NpcMotivation(world.Game.NpcContent, owner, owner.PersonalityIdentity));
            owner.Personality.ChangeExperience(NpcDispositionAxis.Scarcity, 40);
            Check.Equal(true, world.Game.NpcContent.Value("FoodReserve").Importance(
                new NpcMotivation(world.Game.NpcContent, owner, owner.PersonalityIdentity)) > reserve,
                "scarcity raises the actual reserve goal");

            PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", owner, null,
                world.Map, owner.Location.Position, world.Map.LocalTime.TurnCounter));
            Check.Equal(48, owner.Personality.ExperienceValue(NpcDispositionAxis.Scarcity),
                "experienced starvation changes the scale immediately");
            Actor player = NpcIntentSupport.Player(world, 5, 1);
            Check.Equal(0, PersonalitySystem.Disposition(player, NpcDispositionAxis.Scarcity, world.Game.NpcContent),
                "the player has no NPC disposition scales");
            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            Check.Equal(0, PersonalitySystem.Disposition(owner, NpcDispositionAxis.Scarcity, world.Game.NpcContent),
                "disabled personalities suppress disposition effects");
        });
    }
}
