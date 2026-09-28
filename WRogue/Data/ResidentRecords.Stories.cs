namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        public void InferredMissing(Actor actor, NpcFact fact)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            record.Add("inferred:" + fact.EventId, fact.LearnedTurn, "Inferred missing contact with " + fact.SubjectName + ".",
                new ObservedEvent("knowledge_inferred", fact.LearnedTurn, actor.UnmodifiedName, fact.SubjectName, true,
                    subjectId: actor.PersonalityIdentity, otherId: fact.SubjectId, eventId: fact.EventId));
        }
        public void StoryChanged(Actor actor, NpcStory story, int turn, long eventId)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            record.Add("story:" + story.Id + ":" + story.Stage, turn,
                "Story " + story.Template + ": " + story.Stage + ". [story " + story.Id + "]",
                new ObservedEvent("story_stage", turn, actor.UnmodifiedName, null, true,
                    subjectId: actor.PersonalityIdentity, eventId: eventId, causeId: story.CauseId, storyId: story.Id));
        }
    }
}
