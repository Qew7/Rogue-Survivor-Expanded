using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcCollectiveContext
    {
        public readonly RogueGame Game;
        public readonly Actor Leader;
        public readonly IList<Actor> Visible;
        public SocialGroup Group { get { return Leader.SocialGroup; } }
        public NpcKnowledge Knowledge { get { return Leader.Personality.Knowledge; } }
        public NpcFactionPolicy Policy { get { return Game.NpcContent.FactionPolicy(Leader.Faction.ID); } }
        public int Turn { get { return Leader.Location.Map.LocalTime.TurnCounter; } }
        public NpcCollectiveContext(RogueGame game, Actor leader, IList<Actor> visible) { Game = game; Leader = leader; Visible = visible; }
    }
    sealed class NpcCollectiveOffer
    {
        public readonly NpcGroupPlan Plan;
        public readonly int Priority;
        public NpcCollectiveOffer(NpcGroupPlan plan, int priority) { Plan = plan; Priority = priority; }
    }
    enum NpcCollectiveScope { Group, Faction }
    sealed class NpcCollectiveDefinition
    {
        public readonly string Id, EventId;
        public readonly Func<NpcCollectiveContext, NpcCollectiveOffer> Propose;
        public readonly Func<Actor, Actor, NpcGroupPlan, bool> CanCommunicate;
        public readonly Func<Actor, NpcGroupPlan, string> Message;
        public readonly Action<RogueGame, Actor, SignificantEvent> Accept;
        public NpcCollectiveScope Scope;
        public bool RequiresDeliveryReport;
        public string CoordinatorCapability;
        public NpcCollectiveDefinition(string id, string eventId, Func<NpcCollectiveContext, NpcCollectiveOffer> propose,
            Func<Actor, Actor, NpcGroupPlan, bool> canCommunicate, Func<Actor, NpcGroupPlan, string> message,
            Action<RogueGame, Actor, SignificantEvent> accept)
        { Id = id; EventId = eventId; Propose = propose; CanCommunicate = canCommunicate; Message = message; Accept = accept; }
    }
}
