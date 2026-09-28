using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    sealed partial class ResidentRecords
    {
        public void IntentChanged(Actor actor, NpcIntent intent, string state, string reason)
        {
            ResidentRecord record = Register(actor); if (record == null) return;
            int turn = state == "started" ? intent.StartedTurn : intent.FinishedTurn;
            ObservedEvent entry = new ObservedEvent("goal_" + state, turn, actor.UnmodifiedName, intent.TargetName,
                true, false, actor.PersonalityIdentity, intent.TargetId, causeId: intent.CauseId, storyId: intent.StoryId);
            NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
            record.Add("goal:" + intent.Sequence + ":" + state, turn,
                "Intent " + state + ": " + (definition == null ? intent.DefinitionId : definition.Name) + "; target " + intent.TargetName +
                "; " + reason + ". [story " + intent.StoryId + "]", entry);
        }
    }
}
