using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcStory : ActorAction
    {
        readonly NpcIntent intent;
        readonly Actor target;
        readonly ItemFood food;
        public ActionNpcStory(Actor actor, RogueGame game, NpcIntent intent, Actor target = null, ItemFood food = null) : base(actor, game)
        { this.intent = intent; this.target = target; this.food = food; }
        public override bool IsLegal()
        {
            if (!NpcIntentSystem.Enabled(m_Actor) || m_Actor.IsSleeping || intent == null || intent.Finished ||
                intent.Status == NpcIntentStatus.Paused || !m_Actor.Personality.Intents.Contains(intent)) return false;
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
            if (definition == null || turn >= intent.Deadline || turn < intent.NextAttempt || definition.Score(m_Actor, intent) < definition.ThresholdFor(intent)) return false;
            if (definition.Method < NpcIntentMethod.SeekPerson || definition.Method == NpcIntentMethod.Coordinate) return false;
            if (intent.GroupId != Guid.Empty && (m_Actor.SocialGroup == null || m_Actor.SocialGroup.Identity != intent.GroupId)) return false;
            if (definition.Method == NpcIntentMethod.ReachShelter)
                return m_Actor.Location.Map == intent.Destination.Map && m_Game.Rules.GridDistance(m_Actor.Location.Position, intent.Destination.Position) <= 1 &&
                    m_Actor.Location.Map.GetTileAt(m_Actor.Location.Position).IsInside;
            if (definition.Method == NpcIntentMethod.AvoidPerson)
                return intent.LastKnown.Map != m_Actor.Location.Map || m_Game.Rules.GridDistance(m_Actor.Location.Position, intent.LastKnown.Position) >= 5;
            if (definition.Method == NpcIntentMethod.GatherFood && intent.Progress == 0)
            {
                string reason; Inventory ground = m_Actor.Location.Map.GetItemsAt(intent.Destination.Position);
                return intent.Destination.Map == m_Actor.Location.Map && food != null && ground != null && ground.Contains(food) &&
                    m_Game.Rules.GridDistance(m_Actor.Location.Position, intent.Destination.Position) <= 1 && m_Game.Rules.CanActorGetItem(m_Actor, food, out reason);
            }
            Guid id = definition.Method == NpcIntentMethod.GatherFood && intent.Progress == 2 ? intent.CoordinatorId : intent.TargetId;
            if (target == null || target.PersonalityIdentity != id || target.IsSleeping || !NpcIntentSystem.CanSee(m_Game, m_Actor, target) ||
                m_Game.Rules.GridDistance(m_Actor.Location.Position, target.Location.Position) > 1 || m_Game.Rules.AreEnemies(m_Actor, target)) return false;
            return definition.Method != NpcIntentMethod.GatherFood || intent.Progress == 2 || (food != null && NpcFoodSupply.SpareFood(m_Game, m_Actor, target) == food);
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            NpcIntentMethod method = NpcIntentContent.Find(intent.DefinitionId).Method;
            if (method == NpcIntentMethod.GatherFood && intent.Progress == 0)
            {
                long before = m_Actor.Inventory.TotalReceived;
                m_Game.DoTakeItem(m_Actor, intent.Destination.Position, food);
                if (m_Actor.Inventory.TotalReceived <= before) { NpcIntentSystem.Finish(m_Actor, intent, NpcIntentStatus.Failed, "could not acquire the observed supplies"); return; }
                intent.Progress = 1;
                NpcEvents.Publish(m_Game, "supplies_acquired", m_Actor, null, intent.CauseId, intent.StoryId); return;
            }
            if (method == NpcIntentMethod.GatherFood && intent.Progress == 1) { m_Game.DoNpcIntent(m_Actor, target, intent, food); return; }
            string kind = method == NpcIntentMethod.GatherFood ? "supplies_delivered" : method == NpcIntentMethod.SeekPerson ? "reunited" :
                method == NpcIntentMethod.ConfrontPerson ? "confronted" : method == NpcIntentMethod.ReachShelter ?
                    intent.Generated != null && intent.Generated.Value == NpcGoalValue.ProtectHome ? "home_reached" : "shelter_reached" : "withdrew";
            if (target != null) m_Game.DoSay(m_Actor, target, method == NpcIntentMethod.GatherFood ? "The food was delivered." :
                method == NpcIntentMethod.SeekPerson ? "There you are. I was looking for you." :
                m_Actor.Personality.Knowledge.Facts.Exists(f => f.Kind == "promise_broken" && f.EventId == intent.CauseId) ?
                    "You promised to help. What happened?" : "I know what happened. Leave us and our belongings alone.", RogueGame.Sayflags.NONE);
            else m_Game.DoWait(m_Actor);
            NpcIntentSystem.Finish(m_Actor, intent, NpcIntentStatus.Completed, kind == "withdrew" ? "withdrew from the last reported location" : kind);
            NpcEvents.Publish(m_Game, kind, m_Actor, target, intent.CauseId, intent.StoryId);
        }
    }
}
