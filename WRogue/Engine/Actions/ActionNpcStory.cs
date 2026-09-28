using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcTell : ActorAction
    {
        readonly Actor target;
        readonly NpcFact fact;
        public ActionNpcTell(Actor actor, RogueGame game, Actor target, NpcFact fact) : base(actor, game) { this.target = target; this.fact = fact; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && target != null && !target.IsSleeping && fact != null &&
            target.Model.Abilities.IsIntelligent && !target.Model.Abilities.IsUndead && NpcIntentSystem.CanSee(m_Game, m_Actor, target) &&
            m_Game.Rules.GridDistance(m_Actor.Location.Position, target.Location.Position) <= 4 && !m_Game.Rules.AreEnemies(m_Actor, target) &&
            m_Actor.Personality.Knowledge.Facts.Contains(fact) && fact.Confidence >= 40 && fact.Hops < 3 &&
            m_Actor.Location.Map.LocalTime.TurnCounter - fact.EventTurn <= 2 * WorldTime.TURNS_PER_DAY &&
            !m_Actor.Personality.Knowledge.WasTold(fact.EventId, target.PersonalityIdentity); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            string report = fact.Kind == "death" ? fact.SubjectName + " died" : fact.Kind == "requested_food" ?
                fact.SubjectName + " asked for food" : fact.Kind == "food_cache" ? "there was food" :
                fact.OtherId == Guid.Empty ? "there was " + fact.Kind.Replace('_', ' ') : fact.OtherName + " was involved in violence against " + fact.SubjectName;
            m_Game.DoSay(m_Actor, target, (fact.Source == NpcKnowledgeSource.Told ? "I was told that " : "I saw that ") + report +
                " near " + fact.Place.Map.Name + ".", RogueGame.Sayflags.NONE);
            if (target.Personality == null) target.Personality = new PersonalityState();
            NpcKnowledgeSystem.Hear(m_Game, target, m_Actor, fact);
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            m_Actor.Personality.Knowledge.Told(fact.EventId, target.PersonalityIdentity, turn);
            m_Actor.Personality.Knowledge.NextTalkTurn = turn + 30;
            NpcIntentSystem.Publish(m_Game, "rumor_shared", m_Actor, target, fact.EventId, fact.StoryId);
        }
    }
    sealed class ActionNpcGroupPlan : ActorAction
    {
        readonly Actor listener;
        readonly NpcGroupPlan plan;
        public ActionNpcGroupPlan(Actor actor, RogueGame game, Actor listener, NpcGroupPlan plan) : base(actor, game) { this.listener = listener; this.plan = plan; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && plan != null && plan.Destination.Map != null &&
            m_Actor.Location.Map.LocalTime.TurnCounter < plan.Deadline && listener != null && !listener.IsSleeping &&
            m_Actor.SocialGroup != null && m_Actor.SocialGroup.LeaderId == m_Actor.PersonalityIdentity && listener.SocialGroup == m_Actor.SocialGroup &&
            (plan.Kind == "group_shelter" || (plan.Kind == "group_supplies" && listener.PersonalityIdentity == plan.CollectorId && NpcIntentSystem.Enabled(listener))) &&
            (m_Actor.SocialGroup.Plan == null || m_Actor.SocialGroup.Plan.Finished) && NpcIntentSystem.CanSee(m_Game, m_Actor, listener) &&
            m_Game.Rules.GridDistance(m_Actor.Location.Position, listener.Location.Position) <= 4 &&
            !m_Game.Rules.AreEnemies(m_Actor, listener) &&
            Session.Get.NpcDirector.CanOpen(m_Actor.Location.Map, m_Actor.Location.Map.LocalTime.TurnCounter,
                "group:" + m_Actor.SocialGroup.Identity, plan.Destination, plan.CollectorId); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            SocialGroup group = m_Actor.SocialGroup;
            string id = "group:" + group.Identity.ToString("N") + ":" + (group.PlanSequence + 1);
            NpcStory story = Session.Get.NpcDirector.Open(id, plan.Kind, m_Actor, plan.CauseId, plan.Deadline,
                "group:" + group.Identity, plan.Destination, plan.CollectorId);
            if (story == null) return;
            group.PlanSequence++; plan.StoryId = id; group.Plan = plan; group.NextPlanTurn = m_Actor.Location.Map.LocalTime.TurnCounter + 180;
            NpcKnownPerson beneficiary = m_Actor.Personality.Knowledge.Person(plan.BeneficiaryId);
            m_Game.DoSay(m_Actor, listener, plan.Kind == "group_supplies" ?
                "Fetch the food near " + plan.Destination.Map.Name + " for " + (beneficiary == null ? "our companion" : beneficiary.Name) + ". Then report back." :
                "Let's take shelter near " + plan.Destination.Map.Name + ".", RogueGame.Sayflags.NONE);
            if (plan.Kind == "group_supplies")
            {
                NpcIntent coordination = NpcStorySystem.StartKnown(m_Actor, beneficiary, NpcIntentContent.Coordinate,
                    plan.CauseId, id, groupId: group.Identity);
                if (coordination != null) coordination.Status = NpcIntentStatus.Waiting;
            }
            var source = new SignificantEvent(plan.Kind == "group_supplies" ? "supplies_requested" : "shelter_suggested",
                m_Actor, listener, m_Actor.Location.Map, m_Actor.Location.Position, m_Actor.Location.Map.LocalTime.TurnCounter,
                causeId: plan.CauseId, storyId: id) { Task = plan };
            PersonalitySystem.Report(m_Game, source);
        }
    }
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
            if (definition == null || turn >= intent.Deadline || turn < intent.NextAttempt || definition.Score(m_Actor, intent) < definition.Threshold) return false;
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
            return definition.Method != NpcIntentMethod.GatherFood || intent.Progress == 2 || (food != null && NpcIntentSystem.SpareFood(m_Game, m_Actor, target) == food);
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
                NpcIntentSystem.Publish(m_Game, "supplies_acquired", m_Actor, null, intent.CauseId, intent.StoryId); return;
            }
            if (method == NpcIntentMethod.GatherFood && intent.Progress == 1) { m_Game.DoNpcIntent(m_Actor, target, intent, food); return; }
            string kind = method == NpcIntentMethod.GatherFood ? "supplies_delivered" : method == NpcIntentMethod.SeekPerson ? "reunited" :
                method == NpcIntentMethod.ConfrontPerson ? "confronted" : method == NpcIntentMethod.ReachShelter ? "shelter_reached" : "withdrew";
            if (target != null) m_Game.DoSay(m_Actor, target, method == NpcIntentMethod.GatherFood ? "The food was delivered." :
                method == NpcIntentMethod.SeekPerson ? "There you are. I was looking for you." : "I heard about the violence. Stay away from us.", RogueGame.Sayflags.NONE);
            else m_Game.DoWait(m_Actor);
            NpcIntentSystem.Finish(m_Actor, intent, NpcIntentStatus.Completed, kind == "withdrew" ? "withdrew from the last reported location" : kind);
            NpcIntentSystem.Publish(m_Game, kind, m_Actor, target, intent.CauseId, intent.StoryId);
        }
    }
}
