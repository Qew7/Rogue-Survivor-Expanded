using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcObservation
    {
        public readonly RogueGame Game;
        public readonly Actor Owner;
        public readonly SignificantEvent Source;
        public readonly bool Direct, SeesSubject, SeesOther;
        public bool SkipGoals, SkipReplies;
        public NpcObservation(RogueGame game, Actor owner, SignificantEvent source, bool direct)
        {
            Game = game; Owner = owner; Source = source; Direct = direct;
            SeesSubject = source.Subject != null && (owner == source.Subject || NpcKnowledgeSystem.Visible(game, owner, source.Subject.Location));
            SeesOther = source.Other != null && (owner == source.Other || NpcKnowledgeSystem.Visible(game, owner, source.Other.Location));
        }
    }

    static class NpcEventPipeline
    {
        public static void BeforeMemories(NpcObservation observation)
        {
            NpcKnowledgeSystem.ObserveParticipants(observation);
            NpcEventDefinition definition = observation.Game.NpcContent.Event(observation.Source.Kind);
            if (definition == null) return;
            definition.Apply(NpcObservationPhase.Knowledge, observation);
            NpcKnowledgeSystem.RetainEvent(observation, definition);
            definition.Apply(NpcObservationPhase.Relationships, observation);
        }
        public static void AfterMemories(NpcObservation observation)
        {
            if (!NpcIntentSystem.Enabled(observation.Owner)) return;
            PersonalityState state = observation.Owner.Personality;
            if (observation.Source.Id > 0 && observation.Source.Id <= state.LastIntentEventId) return;
            state.LastIntentEventId = observation.Source.Id;
            NpcEventDefinition definition = observation.Game.NpcContent.Event(observation.Source.Kind);
            if (definition != null) definition.Apply(NpcObservationPhase.Goals, observation);
            if (!observation.SkipGoals) NpcGoalGenerator.Refresh(observation.Game, observation.Owner);
            if (definition != null && !observation.SkipReplies)
            { definition.Apply(NpcObservationPhase.Responses, observation); definition.Apply(NpcObservationPhase.Replies, observation); }
        }
    }
}
