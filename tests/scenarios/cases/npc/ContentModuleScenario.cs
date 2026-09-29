using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class ContentModuleScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/content-module", () => TownScenarioFactory.Arena(4690, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            world.Game.NpcContent = PersonalityContent.Create(new NpcRestContent()).Content;
            Actor owner = NpcIntentSupport.Actor(world, "resting", 1, 1, "restful"); owner.StaminaPoints = Rules.STAMINA_MIN_FOR_ACTIVITY;
            NpcGoalGenerator.Refresh(world.Game, owner);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "take_breath");
            Check.Equal("stamina", goal.Generated.DefinitionId, "goal uses a stable module ID without an enum addition");
            Check.Equal(90, goal.Generated.Importance, "module trait parameters contribute through the motivation contract");
            Check.Equal(true, goal.Generated.ResultState.Extended, "symbolic result extends beyond the legacy word");
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(Rules.STAMINA_MIN_FOR_ACTIVITY + Rules.STAMINA_REGEN_WAIT, owner.StaminaPoints, "module executor invokes real waiting and stamina regeneration");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "common lifecycle completes the extension goal");
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "regained_stamina"), "extension event passes through the common pipeline");
            Check.Equal(1, owner.Personality.Memories.Count, "event creates the module memory once");
            Check.Equal(owner.PersonalityIdentity, owner.Personality.Memories[0].SubjectId, "memory keeps the actual participant");
            NpcGoalGenerator.Refresh(world.Game, owner);
            Check.Equal(1, owner.Personality.Intents.Count, "satisfied state and cooldown prevent duplicates");
            world.Map.LocalTime.TurnCounter = owner.Personality.Memories[0].ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, owner.Personality.HasTrait("rested_resolve"), "extension memory resolves to its registered advanced trait");
            Check.Throws<ArgumentException>(() => PersonalitySystem.Report(world.Game, new SignificantEvent("regained_stamina", owner, null,
                world.Map, owner.Location.Position, world.Map.LocalTime.TurnCounter)), "invalid event payload is rejected");
        });
    }
}
