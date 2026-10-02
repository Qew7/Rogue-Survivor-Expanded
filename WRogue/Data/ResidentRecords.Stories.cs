using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        public void LinkStory(Actor actor, string parent, string child, long cause)
        {
            if (parent == null || child == null || parent == child) return;
            ResidentRecord record = Register(actor); if (record == null) return;
            int turn = actor.Location.Map.LocalTime.TurnCounter;
            record.Add("link:" + parent + ":" + child, turn, actor.UnmodifiedName + " found another lead for an ongoing goal.",
                new ObservedEvent("story_link", turn, actor.UnmodifiedName, null, true, causeId: cause, storyId: child));
        }
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
            NpcIntentDefinition definition = NpcContentCatalog.Default.Capability(story.Template);
            string goal = definition == null ? StoryGoal(story.Template) : definition.Name.ToLowerInvariant();
            string progress = story.Stage == "completed" ? "came to an end successfully" :
                story.Stage == "failed" ? "failed" : story.Stage == "abandoned" ? "was abandoned" :
                story.Stage == "contacted" ? "reached the person involved" : "began";
            record.Add("story:" + story.Id + ":" + story.Stage, turn,
                actor.UnmodifiedName + "'s effort to " + goal + " " + progress + ".",
                new ObservedEvent("story_stage", turn, actor.UnmodifiedName, null, true,
                    subjectId: actor.PersonalityIdentity, eventId: eventId, causeId: story.CauseId, storyId: story.Id));
        }

        static string StoryGoal(string template)
        {
            if (template == "group_supplies") return "gather supplies for the group";
            if (template == "group_shelter") return "reach a safe shelter with the group";
            if (template == "faction_supplies") return "gather supplies for the faction";
            if (template == "faction_shelter") return "reach shelter with the faction";
            return Humanize(template);
        }
    }
}
