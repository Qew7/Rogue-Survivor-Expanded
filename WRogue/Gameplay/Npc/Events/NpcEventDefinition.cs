using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum NpcObservationPhase { Knowledge, Relationships, Goals, Responses, Replies }
    [Flags] enum NpcRecordCategory { None = 0, Memories = 1, Combat = 2, Help = 4, Encounters = 8, World = 16, Life = 32, Intentions = 64 }
    [Flags] enum NpcEventFields { None = 0, Subject = 1, Other = 2, Resource = 4, ResourcePlace = 8, Task = 16, PositiveUnits = 32 }
    [Flags] enum NpcReportActorRole { None = 0, Subject = 1, Other = 2, Both = Subject | Other }
    enum NpcSelfReportTone { Neutral, Helpful, Harmful }

    sealed class NpcEventDefinition
    {
        public readonly string Id;
        public readonly NpcRecordCategory Categories;
        public readonly bool RetainFact;
        public readonly Func<ObservedEvent, string> Describe;
        public readonly Func<NpcFact, string> DescribeReport;
        public readonly NpcEventFields Required;
        public readonly bool Private;
        public Func<Actor, SignificantEvent, bool> CanWitness, CanObserve;
        public Func<SignificantEvent, Actor> PrivateAudience;
        public bool OncePerSubject, ProvesDeath, AudibleReport;
        public NpcReportActorRole ReportActorRole = NpcReportActorRole.Subject;
        public NpcSelfReportTone SelfReportTone;
        public Func<Actor, Actor, bool> CanReply;
        public NpcPlayerReply PlayerReply;
        public Func<djack.RogueSurvivor.Engine.RogueGame, NpcStory, SignificantEvent, string> StoryStage;
        public Func<IList<NpcFact>, string> SummarizeReports;
        public bool ReportConclusion;
        public string[] ReportDisputesKinds;
        public int ReportPriority;
        readonly List<Action<NpcObservation>>[] observers = {
            new List<Action<NpcObservation>>(), new List<Action<NpcObservation>>(),
            new List<Action<NpcObservation>>(), new List<Action<NpcObservation>>(), new List<Action<NpcObservation>>() };
        readonly List<Action<NpcReportContext>> reports = new List<Action<NpcReportContext>>();
        readonly List<Action<djack.RogueSurvivor.Engine.RogueGame, SignificantEvent>> completions = new List<Action<djack.RogueSurvivor.Engine.RogueGame, SignificantEvent>>();
        bool frozen;
        public NpcEventDefinition(string id, NpcRecordCategory categories = NpcRecordCategory.None, bool retainFact = false,
            Func<ObservedEvent, string> describe = null, Func<NpcFact, string> describeReport = null,
            NpcEventFields required = NpcEventFields.None, bool isPrivate = false)
        { Id = id; Categories = categories; RetainFact = retainFact; Describe = describe; DescribeReport = describeReport; Required = required; Private = isPrivate; }
        internal void Subscribe(NpcObservationPhase phase, Action<NpcObservation> observer)
        { if (frozen) throw new InvalidOperationException("Event definition is frozen."); if (observer == null) throw new ArgumentNullException("observer"); observers[(int)phase].Add(observer); }
        internal void SubscribeReport(Action<NpcReportContext> observer)
        { if (frozen) throw new InvalidOperationException("Event definition is frozen."); if (observer == null) throw new ArgumentNullException("observer"); reports.Add(observer); }
        public void Hear(NpcReportContext report) { foreach (Action<NpcReportContext> observer in reports) observer(report); }
        internal void SubscribeCompletion(Action<djack.RogueSurvivor.Engine.RogueGame, SignificantEvent> observer)
        { if (frozen) throw new InvalidOperationException("Event definition is frozen."); if (observer == null) throw new ArgumentNullException("observer"); completions.Add(observer); }
        public void Completed(djack.RogueSurvivor.Engine.RogueGame game, SignificantEvent source)
        { foreach (var observer in completions) observer(game, source); }
        internal void Freeze() { frozen = true; }
        public void Apply(NpcObservationPhase phase, NpcObservation observation)
        { foreach (Action<NpcObservation> observer in observers[(int)phase]) observer(observation); }
        public void Validate(SignificantEvent source)
        {
            if (((Required & NpcEventFields.Subject) != 0 && source.Subject == null) ||
                ((Required & NpcEventFields.Other) != 0 && source.Other == null) ||
                ((Required & NpcEventFields.Resource) != 0 && String.IsNullOrEmpty(source.Resource)) ||
                ((Required & NpcEventFields.ResourcePlace) != 0 && source.ResourcePlace.Map == null) ||
                ((Required & NpcEventFields.Task) != 0 && source.Task == null) ||
                ((Required & NpcEventFields.PositiveUnits) != 0 && source.Units <= 0))
                throw new ArgumentException("Missing required payload for NPC event: " + Id);
        }
    }

    // The event owns the player's two possible responses; the conversation UI only invokes this contract.
    sealed class NpcPlayerReply
    {
        public readonly string Prompt, YesKind, NoKind, YesText, NoText;
        public NpcPlayerReply(string prompt, string yesKind, string noKind, string yesText, string noText)
        { Prompt = prompt; YesKind = yesKind; NoKind = noKind; YesText = yesText; NoText = noText; }
    }
}
