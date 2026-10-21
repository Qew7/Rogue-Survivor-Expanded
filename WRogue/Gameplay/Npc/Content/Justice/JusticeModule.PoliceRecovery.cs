using System;
using System.Collections.Generic;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class JusticeModule
    {
        const string PoliceContactFact = "police.recovery_contacted";
        const string PoliceDeliveryFact = "police.recovery_delivered";
        const string PoliceSurrenderFact = "police.recovery_surrendered";

        void RegisterPoliceRecovery(NpcCatalogBuilder c)
        {
            c.Fact(PoliceContactFact); c.Fact(PoliceDeliveryFact); c.Fact(PoliceSurrenderFact);
            c.Value(new NpcValueDefinition("PoliceRecovery", "Investigate a reported theft and seek restitution",
                null, m => 62 + m.Law + m.Courage / 5 + m.Compassion / 4 - m.Fear / 5, true)
                { IdentitySuffix = g => ":" + g.Resource + ":" + g.ObligationId });
            c.Value(new NpcValueDefinition("PoliceDelivery", "Return recovered property to its claimant",
                null, m => 70 + m.Law + m.Compassion / 3, true)
                { IdentitySuffix = g => ":" + g.ItemId.ToString("N") });
            var contact = new NpcIntentDefinition("police_contact_thief", "Question a suspect or request stolen goods", 2 * WorldTime.TURNS_PER_DAY, 90)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(PoliceContactFact), AllowQuestions = true };
            var deliver = new NpcIntentDefinition("police_deliver_property", "Return recovered property", 2 * WorldTime.TURNS_PER_DAY, 90)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(PoliceDeliveryFact) };
            var surrender = new NpcIntentDefinition("police_surrender_property", "Hand over property I agreed to return", 180, 0)
                { ResultFacts = (catalog, g) => catalog.Facts.Mask(PoliceSurrenderFact),
                  AssignedScore = (catalog, owner, target) => 100 };
            c.Capability(contact); c.Capability(deliver); c.Capability(surrender);
            c.OperatorSource(new NpcOperatorSource("police.recovery.contact", catalog => catalog.Facts.Mask(PoliceContactFact), d => {
                if (d.Goal.Generated == null || d.Goal.LastKnown.Map == null) return;
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                var done = d.Catalog.Facts.Mask(PoliceContactFact);
                d.Add("police.recovery.contact", d.Goal.LastKnown, d.Goal.TargetId,
                    (NpcPlanningState)at, done, done, default(NpcPlanningState), 1);
            }));
            c.OperatorSource(new NpcOperatorSource("police.recovery.delivery", catalog => catalog.Facts.Mask(PoliceDeliveryFact), d => {
                if (d.Goal.Generated == null || d.Goal.LastKnown.Map == null) return;
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                var done = d.Catalog.Facts.Mask(PoliceDeliveryFact);
                d.Add("police.recovery.deliver", d.Goal.LastKnown, d.Goal.TargetId,
                    (NpcPlanningState)at, done, done, default(NpcPlanningState), 1);
            }));
            c.OperatorSource(new NpcOperatorSource("police.recovery.surrender", catalog => catalog.Facts.Mask(PoliceSurrenderFact), d => {
                if (d.Goal.LastKnown.Map == null) return;
                ulong at = d.At(d.Goal.LastKnown); if (at == 0) return;
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                var done = d.Catalog.Facts.Mask(PoliceSurrenderFact);
                d.Add("police.recovery.surrender", d.Goal.LastKnown, d.Goal.TargetId,
                    (NpcPlanningState)at, done, done, default(NpcPlanningState), 1);
            }));
            c.Operator(new NpcOperatorDefinition("police.recovery.contact", null, PoliceContact,
                archiveText: step => "question a suspect or request property"));
            c.Operator(new NpcOperatorDefinition("police.recovery.deliver", null, PoliceDeliver,
                archiveText: step => "return recovered property"));
            c.Operator(new NpcOperatorDefinition("police.recovery.surrender", null, PoliceSurrender,
                archiveText: step => "hand over property to an officer"));

            c.Event(new NpcEventDefinition("police_theft_questioned", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " asked " + (e.Other ?? "someone") + " about a reported theft.",
                f => (f.ReportSubject ?? "An officer") + " questioned " + (f.ReportOther ?? "someone") + " about a reported theft")
                { AudibleReport = true, PlayerReply = new NpcPlayerReply("a reported theft", "police_theft_admitted",
                    "police_theft_denied", "I took it.", "I did not take it.", directOnly: true) });
            c.Event(new NpcEventDefinition("police_theft_admitted", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " admitted taking property to " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "Someone") + " admitted taking property to " + (f.ReportOther ?? "an officer"))
                { ReportActorRole = NpcReportActorRole.Subject, SelfReportTone = NpcSelfReportTone.Harmful });
            c.Event(new NpcEventDefinition("police_theft_denied", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " denied a theft reported to " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "Someone") + " denied " + (f.ReportOther ?? "an officer") + "'s theft allegation")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_return_requested", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " asked " + (e.Other ?? "someone") + " to return property from a reported theft.",
                f => (f.ReportSubject ?? "An officer") + " asked " + (f.ReportOther ?? "someone") + " to return reported stolen property")
                { AudibleReport = true, PlayerReply = new NpcPlayerReply("the property you took", "police_return_accepted",
                    "police_return_refused", "I will hand it over.", "I will not hand it over.", directOnly: true) });
            c.Event(new NpcEventDefinition("police_return_accepted", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " agreed to hand property to " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "Someone") + " agreed to hand property to " + (f.ReportOther ?? "an officer"))
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_return_refused", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " refused " + (e.Other ?? "an officer") + "'s request to return property.",
                f => (f.ReportSubject ?? "Someone") + " refused " + (f.ReportOther ?? "an officer") + "'s request to return property")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_goods_missing", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " said they no longer had the reported stolen property.",
                f => (f.ReportSubject ?? "Someone") + " said the reported stolen property was gone")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_goods_surrendered", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " handed reported stolen property to " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "Someone") + " handed reported stolen property to " + (f.ReportOther ?? "an officer"))
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_escort_offered", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " asked " + (e.Other ?? "a suspect") + " to accompany the property return.",
                f => (f.ReportSubject ?? "An officer") + " asked " + (f.ReportOther ?? "a suspect") + " to accompany a property return")
                { AudibleReport = true });
            c.Event(new NpcEventDefinition("police_escort_accepted", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "A suspect") + " agreed to accompany " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "A suspect") + " agreed to accompany " + (f.ReportOther ?? "an officer"))
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_escort_refused", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "A suspect") + " declined to accompany " + (e.Other ?? "an officer") + ".",
                f => (f.ReportSubject ?? "A suspect") + " declined to accompany " + (f.ReportOther ?? "an officer"))
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_escort_started", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " took " + (e.Other ?? "a suspect") + " along to return stolen property.",
                f => (f.ReportSubject ?? "An officer") + " took " + (f.ReportOther ?? "a suspect") + " along to return stolen property")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_escort_finished", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " finished escorting " + (e.Other ?? "a suspect") + " after returning property.",
                f => (f.ReportSubject ?? "An officer") + " finished escorting " + (f.ReportOther ?? "a suspect") + " after returning property")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_escort_ended", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "An officer") + " ended the escort of " + (e.Other ?? "a suspect") + " without returning the property.",
                f => (f.ReportSubject ?? "An officer") + " ended the escort of " + (f.ReportOther ?? "a suspect") + " without returning property")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.Event(new NpcEventDefinition("police_goods_delivered", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "An officer") + " returned recovered property to " + (e.Other ?? "its claimant") + ".",
                f => (f.ReportSubject ?? "An officer") + " returned recovered property to " + (f.ReportOther ?? "its claimant"))
                { ReportActorRole = NpcReportActorRole.Subject, ReportConclusion = true });
            c.Event(new NpcEventDefinition("police_goods_thanked", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " thanked " + (e.Other ?? "an officer") + " for returning property.",
                f => (f.ReportSubject ?? "Someone") + " thanked " + (f.ReportOther ?? "an officer") + " for returning property")
                { ReportActorRole = NpcReportActorRole.Subject });
            c.On("police_theft_questioned", NpcObservationPhase.Replies, PoliceQuestionReply);
            c.On("police_return_requested", NpcObservationPhase.Replies, PoliceReturnReply);
            c.On("police_escort_offered", NpcObservationPhase.Replies, PoliceEscortReply);
            c.On("police_goods_delivered", NpcObservationPhase.Relationships, PoliceDeliveryReaction);
            c.AfterEvent("police_return_accepted", PoliceStartSurrender);
            c.AfterEvent("police_goods_surrendered", PoliceOfferEscort);
            c.AfterEvent("police_escort_accepted", PoliceStartEscort);
            c.AfterEvent("police_goods_delivered", PoliceFinishEscort);
            c.Clock(NpcClockPhase.MapTurn, clock => PoliceExpireEscort(clock.Game, clock.Owner));
            c.OnReport("police_goods_surrendered", report => NpcReputation.Help(report));
            c.OnReport("police_goods_delivered", report => NpcReputation.Help(report));
        }

        static NpcFact PoliceFact(Actor actor, long id)
        { return actor.Personality.Knowledge.Facts.Find(f => f.EventId == id); }

        static bool PoliceAdmitted(NpcFact theft)
        { return theft.Source == NpcKnowledgeSource.Told && theft.SourceId == theft.SubjectId ||
            theft.Source == NpcKnowledgeSource.Witness || theft.Source == NpcKnowledgeSource.Participant; }

        static void EvaluatePoliceRecovery(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor officer = context.Owner;
            if (officer.Faction == null || officer.Faction.ID != (int)GameFactions.IDs.ThePolice) return;
            foreach (NpcFact theft in officer.Personality.Knowledge.Facts)
            {
                if ((theft.Kind != "base_theft" && theft.Kind != "cache_theft" && theft.Kind != "robbery_taken") || !theft.NamesSubject ||
                    theft.SubjectId == officer.PersonalityIdentity || theft.Confidence < 50 ||
                    context.Turn - theft.EventTurn > 2 * WorldTime.TURNS_PER_DAY) continue;
                NpcKnownPerson suspect = context.FindPerson(p => p.Id == theft.SubjectId);
                if (suspect == null || suspect.Dead || suspect.Hostile || suspect.Place.Map == null) continue;
                NpcFact question = officer.Personality.Knowledge.Facts.Find(f =>
                    f.Kind == "police_theft_questioned" && f.CauseId == theft.EventId &&
                    f.SubjectId == officer.PersonalityIdentity);
                NpcFact demand = officer.Personality.Knowledge.Facts.Find(f =>
                    f.Kind == "police_return_requested" && f.CauseId == theft.EventId &&
                    f.SubjectId == officer.PersonalityIdentity);
                if (demand != null) continue;
                bool admitted = PoliceAdmitted(theft) || question != null && officer.Personality.Knowledge.Facts.Exists(f =>
                    f.Kind == "police_theft_admitted" && f.CauseId == question.EventId && f.SubjectId == suspect.Id);
                if (!admitted && question != null) continue;
                offers.Add(suspect, "PoliceRecovery", "police_contact_thief", 0, 1, 100,
                    theft.Confidence, theft.EventId, theft.StoryId, admitted ? "return" : "question",
                    obligation: theft.EventId);
            }
            foreach (Item item in officer.Inventory.Items)
            {
                if (!item.IsStolen || item.StolenFromLeaderId == Guid.Empty) continue;
                Guid claimant = item.StolenFromLeaderId, group = item.StolenFromGroupId;
                NpcKnownPerson recipient = context.FindPerson(p => p.Id == claimant && !p.Dead && p.Place.Map != null);
                if (recipient == null && group != Guid.Empty)
                    recipient = context.FindPerson(p => p.GroupId == group && p.Id != officer.PersonalityIdentity &&
                        !p.Dead && p.Place.Map != null);
                if (recipient != null)
                    offers.Add(recipient, "PoliceDelivery", "police_deliver_property", 0, 1, 100,
                        100, item.TheftEventId, item.TheftStoryId, "property", itemId: item.StoryIdentity);
            }
        }

        static ActorAction PoliceContact(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            NpcFact theft = PoliceFact(c.Owner, c.Goal.Generated.ObligationId);
            if (theft == null || !c.NearPerson(c.Goal.TargetId)) return null;
            bool demand = c.Goal.Generated.Resource == "return";
            string speech = demand ?
                (theft.Source == NpcKnowledgeSource.Witness ? "I saw you take property. Please hand it over." :
                 theft.Source == NpcKnowledgeSource.Participant || theft.SourceId == theft.SubjectId ?
                    "You admitted taking property. Please hand it over." :
                    "You said you took property. Please hand it over.") :
                "I heard you took property. What happened?";
            int turn = c.Owner.Location.Map.LocalTime.TurnCounter;
            if (!c.Owner.Personality.Knowledge.CanRequest(c.Goal.Sequence, c.Target.PersonalityIdentity, speech, turn)) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) && c.Owner.Personality.Knowledge.CanRequest(
                c.Goal.Sequence, c.Target.PersonalityIdentity, speech, c.Owner.Location.Map.LocalTime.TurnCounter), () => {
                c.Game.DoSay(c.Owner, c.Target, speech, RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                    theft.EventId, theft.StoryId);
                c.Owner.Personality.Knowledge.Requested(c.Goal.Sequence, c.Target.PersonalityIdentity, speech,
                    c.Owner.Location.Map.LocalTime.TurnCounter);
                PersonalitySystem.Report(c.Game, new SignificantEvent(demand ? "police_return_requested" : "police_theft_questioned",
                    c.Owner, c.Target, c.Owner.Location.Map, c.Owner.Location.Position,
                    c.Owner.Location.Map.LocalTime.TurnCounter, causeId: theft.EventId, storyId: theft.StoryId)
                    { Resource = theft.Resource, Units = theft.Units });
                c.Done(c.Game.NpcContent.Facts.Mask(PoliceContactFact));
            });
        }

        static void PoliceQuestionReply(NpcObservation observation)
        {
            SignificantEvent question = observation.Source;
            Actor suspect = observation.Owner;
            if (suspect != question.Other || question.Subject == null || !NpcIntentSystem.Enabled(suspect)) return;
            NpcFact theft = PoliceFact(suspect, question.CauseId);
            bool didIt = theft != null && theft.Source == NpcKnowledgeSource.Participant &&
                theft.SubjectId == suspect.PersonalityIdentity;
            bool admit = didIt && (PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Law) +
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Compassion) -
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Courage) >= 0 ||
                suspect.Personality.HasTrait("honest"));
            NpcReplies.Reply(suspect, question.Subject, question,
                admit ? "police_theft_admitted" : "police_theft_denied",
                admit ? "I took it." : "I did not take it.");
        }

        static Item PoliceStolenItem(Actor holder, long theftId)
        {
            foreach (Item item in holder.Inventory.Items)
                if (item.IsStolen && item.TheftEventId == theftId) return item;
            return null;
        }

        static void PoliceReturnReply(NpcObservation observation)
        {
            SignificantEvent request = observation.Source;
            Actor suspect = observation.Owner;
            if (suspect != request.Other || request.Subject == null || !NpcIntentSystem.Enabled(suspect)) return;
            Item item = PoliceStolenItem(suspect, request.CauseId);
            if (item == null)
            { NpcReplies.Reply(suspect, request.Subject, request, "police_goods_missing", "I no longer have it."); return; }
            int willingness = PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Law) +
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Compassion) -
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Courage) +
                PersonalitySystem.Disposition(suspect, NpcPersonalityAxis.Patience, observation.Game.NpcContent) +
                (suspect.Personality.Person(request.Subject.PersonalityIdentity) == null ? 0 :
                 suspect.Personality.Person(request.Subject.PersonalityIdentity).Fear);
            NpcReplies.Reply(suspect, request.Subject, request,
                willingness >= 0 ? "police_return_accepted" : "police_return_refused",
                willingness >= 0 ? "I will hand it over." : "I will not hand it over.");
        }

        static void PoliceStartSurrender(RogueGame game, SignificantEvent accepted)
        {
            Actor suspect = accepted.Subject, officer = accepted.Other;
            if (suspect == null || officer == null) return;
            NpcFact request = PoliceFact(officer, accepted.CauseId);
            if (request == null || request.Kind != "police_return_requested") return;
            if (suspect.IsPlayer)
            {
                Item item = PoliceStolenItem(suspect, request.CauseId);
                string reason;
                if (item == null)
                {
                    PersonalitySystem.Report(game, new SignificantEvent("police_goods_missing", suspect, officer,
                        suspect.Location.Map, suspect.Location.Position, suspect.Location.Map.LocalTime.TurnCounter,
                        causeId: request.CauseId, storyId: accepted.StoryId));
                    return;
                }
                if (item.IsEquipped)
                {
                    if (!game.Rules.CanActorUnequipItem(suspect, item, out reason)) return;
                    game.DoUnequipItem(suspect, item);
                }
                if (!game.Rules.CanActorGiveItemTo(suspect, officer, item, out reason)) return;
                long before = officer.Inventory.TotalReceived;
                long theftId = item.TheftEventId;
                Guid itemId = item.StoryIdentity, claimant = item.StolenFromLeaderId, group = item.StolenFromGroupId;
                game.DoGiveItemTo(suspect, officer, item);
                if (officer.Inventory.TotalReceived > before)
                    PersonalitySystem.Report(game, new SignificantEvent("police_goods_surrendered", suspect, officer,
                        suspect.Location.Map, suspect.Location.Position, suspect.Location.Map.LocalTime.TurnCounter,
                        causeId: theftId, storyId: accepted.StoryId)
                        { ItemId = itemId, ClaimantId = claimant, ClaimantGroupId = group, Units = 1 });
                return;
            }
            if (!NpcIntentSystem.Enabled(suspect)) return;
            NpcKnownPerson known = suspect.Personality.Knowledge.Person(officer.PersonalityIdentity);
            if (known == null || known.Place.Map == null) return;
            NpcStorySystem.StartKnown(game.NpcContent, suspect, known,
                game.NpcContent.Capability("police_surrender_property"), accepted.Id, accepted.StoryId);
        }

        static ActorAction PoliceSurrender(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            NpcFact accepted = PoliceFact(c.Owner, c.Goal.CauseId);
            NpcFact request = accepted == null ? null : PoliceFact(c.Owner, accepted.CauseId);
            if (accepted == null || request == null || request.Kind != "police_return_requested" ||
                !c.NearPerson(c.Goal.TargetId)) return null;
            Item item = PoliceStolenItem(c.Owner, request.CauseId);
            if (item == null) return c.Action(() => c.NearPerson(c.Goal.TargetId), () => {
                c.Game.DoSay(c.Owner, c.Target, "I no longer have it.", RogueGame.Sayflags.IS_STORY,
                    request.CauseId, c.Goal.StoryId);
                PersonalitySystem.Report(c.Game, new SignificantEvent("police_goods_missing", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: request.CauseId, storyId: c.Goal.StoryId));
                NpcIntentSystem.Finish(c.Game.NpcContent, c.Owner, c.Goal, NpcIntentStatus.Failed, "property was no longer carried");
            });
            if (item.IsEquipped) return new ActionUnequipItem(c.Owner, c.Game, item);
            string reason;
            if (!c.Game.Rules.CanActorGiveItemTo(c.Owner, c.Target, item, out reason)) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) &&
                c.Game.Rules.CanActorGiveItemTo(c.Owner, c.Target, item, out reason), () => {
                long before = c.Target.Inventory.TotalReceived;
                long theftId = item.TheftEventId;
                Guid itemId = item.StoryIdentity;
                Guid claimant = item.StolenFromLeaderId, group = item.StolenFromGroupId;
                c.Game.DoGiveItemTo(c.Owner, c.Target, item);
                if (c.Target.Inventory.TotalReceived <= before) return;
                PersonalitySystem.Report(c.Game, new SignificantEvent("police_goods_surrendered", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: theftId, storyId: c.Goal.StoryId)
                    { ItemId = itemId, ClaimantId = claimant, ClaimantGroupId = group, Units = 1 });
                c.Done(c.Game.NpcContent.Facts.Mask(PoliceSurrenderFact));
            });
        }

        static ActorAction PoliceDeliver(NpcExecutionContext execution)
        {
            var c = new NpcActionContext(execution);
            if (!c.NearPerson(c.Goal.TargetId)) return null;
            Item item = null;
            foreach (Item held in c.Owner.Inventory.Items)
                if (held.StoryIdentity == c.Goal.Generated.ItemId && held.IsStolen)
                { item = held; break; }
            if (item == null || c.Target.PersonalityIdentity != item.StolenFromLeaderId &&
                (c.Target.SocialGroup == null || c.Target.SocialGroup.Identity != item.StolenFromGroupId)) return null;
            if (item.IsEquipped) return new ActionUnequipItem(c.Owner, c.Game, item);
            string reason;
            if (!c.Game.Rules.CanActorGiveItemTo(c.Owner, c.Target, item, out reason)) return null;
            return c.Action(() => c.NearPerson(c.Goal.TargetId) &&
                c.Game.Rules.CanActorGiveItemTo(c.Owner, c.Target, item, out reason), () => {
                long before = c.Target.Inventory.TotalReceived;
                long theftId = item.TheftEventId;
                Guid claimant = item.StolenFromLeaderId, group = item.StolenFromGroupId, itemId = item.StoryIdentity;
                NpcFact handover = c.Owner.Personality.Knowledge.Facts.Find(f =>
                    f.Kind == "police_goods_surrendered" && f.ItemId == itemId && f.CauseId == theftId);
                c.Game.DoGiveItemTo(c.Owner, c.Target, item);
                if (c.Target.Inventory.TotalReceived <= before) return;
                // A direct handover introduces the officer before the recipient records the event.
                c.Target.Personality.Opinion(c.Owner.PersonalityIdentity, c.Owner.UnmodifiedName);
                foreach (Item received in c.Target.Inventory.Items)
                    if (received.IsStolen && received.TheftEventId == theftId &&
                        received.StolenFromLeaderId == claimant && received.StolenFromGroupId == group)
                        received.ClearTheft();
                PersonalitySystem.Report(c.Game, new SignificantEvent("police_goods_delivered", c.Owner, c.Target,
                    c.Owner.Location.Map, c.Owner.Location.Position, c.Owner.Location.Map.LocalTime.TurnCounter,
                    causeId: handover == null ? theftId : handover.EventId, storyId: c.Goal.StoryId)
                    { ItemId = itemId, ClaimantId = claimant, ClaimantGroupId = group, Units = 1 });
                c.Done(c.Game.NpcContent.Facts.Mask(PoliceDeliveryFact));
            });
        }

        static void PoliceDeliveryReaction(NpcObservation observation)
        {
            SignificantEvent delivery = observation.Source;
            Actor recipient = observation.Owner;
            if (recipient != delivery.Other || delivery.Subject == null) return;
            recipient.Personality.Opinion(delivery.Subject.PersonalityIdentity, delivery.Subject.UnmodifiedName)
                .AdjustSocial(trust: 12, grievance: -5);
            if (NpcIntentSystem.Enabled(recipient))
                NpcReplies.Reply(recipient, delivery.Subject, delivery, "police_goods_thanked",
                    "Thank you for bringing this back.");
        }

        static void PoliceOfferEscort(RogueGame game, SignificantEvent handover)
        {
            Actor suspect = handover.Subject, officer = handover.Other;
            if (suspect == null || officer == null || suspect.IsPlayer ||
                !PoliceCanEscort(game, officer, suspect) ||
                !officer.Inventory.Items.Any(item => item.StoryIdentity == handover.ItemId && item.IsStolen)) return;
            game.DoSay(officer, suspect, "Come with me while I return this property.",
                RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST, handover.Id, handover.StoryId);
            PersonalitySystem.Report(game, new SignificantEvent("police_escort_offered", officer, suspect,
                officer.Location.Map, officer.Location.Position, officer.Location.Map.LocalTime.TurnCounter,
                causeId: handover.Id, storyId: handover.StoryId) { ItemId = handover.ItemId });
        }

        static void PoliceEscortReply(NpcObservation observation)
        {
            Actor suspect = observation.Owner, officer = observation.Source.Subject;
            if (suspect != observation.Source.Other || officer == null || !NpcIntentSystem.Enabled(suspect)) return;
            int willingness = 10 + PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Law) +
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Compassion) -
                PersonalitySystem.Bias(suspect, NpcPersonalityAxis.Courage) +
                PersonalitySystem.Attitude(suspect, officer) / 4;
            NpcReplies.Reply(suspect, officer, observation.Source,
                willingness >= 0 ? "police_escort_accepted" : "police_escort_refused",
                willingness >= 0 ? "I'll come with you." : "I'll go my own way.");
        }

        static void PoliceStartEscort(RogueGame game, SignificantEvent accepted)
        {
            Actor suspect = accepted.Subject, officer = accepted.Other;
            if (suspect == null || officer == null || suspect.IsDead || officer.IsDead) return;
            NpcFact offer = PoliceFact(officer, accepted.CauseId);
            NpcFact handover = offer == null ? null : PoliceFact(officer, offer.CauseId);
            if (offer == null || offer.Kind != "police_escort_offered" || handover == null ||
                handover.Kind != "police_goods_surrendered" ||
                !officer.Inventory.Items.Any(item => item.StoryIdentity == handover.ItemId && item.IsStolen) ||
                !PoliceCanEscort(game, officer, suspect)) return;
            game.DoTakeLead(officer, suspect);
            PersonalitySystem.Report(game, new SignificantEvent("police_escort_started", officer, suspect,
                officer.Location.Map, officer.Location.Position, officer.Location.Map.LocalTime.TurnCounter,
                causeId: accepted.Id, storyId: accepted.StoryId) { ItemId = handover.ItemId });
        }

        static void PoliceFinishEscort(RogueGame game, SignificantEvent delivery)
        {
            Actor officer = delivery.Subject;
            if (officer == null) return;
            IEnumerable<Actor> followers = officer.Followers;
            if (followers == null) return;
            foreach (Actor follower in new List<Actor>(followers))
            {
                NpcFact escort = officer.Personality.Knowledge.Facts.Find(f =>
                    f.Kind == "police_escort_started" && f.OtherId == follower.PersonalityIdentity &&
                    f.ItemId == delivery.ItemId);
                if (escort == null) continue;
                officer.RemoveFollower(follower);
                PersonalitySystem.Report(game, new SignificantEvent("police_escort_finished", officer, follower,
                    officer.Location.Map, officer.Location.Position, officer.Location.Map.LocalTime.TurnCounter,
                    causeId: delivery.Id, storyId: delivery.StoryId) { ItemId = delivery.ItemId });
            }
        }

        static bool PoliceCanEscort(RogueGame game, Actor officer, Actor suspect)
        {
            string reason;
            if (game.Rules.CanActorTakeLead(officer, suspect, out reason)) return true;
            // A single temporary escort does not require the Leadership skill.
            // All other ordinary recruitment restrictions still apply.
            return reason == "can't lead" && officer.CountFollowers == 0 && !suspect.HasLeader &&
                suspect.CountFollowers == 0 && !suspect.IsPlayer && suspect.Faction != null &&
                (suspect.Faction == officer.Faction || !suspect.Faction.LeadOnlyBySameFaction);
        }

        static void PoliceExpireEscort(RogueGame game, Actor officer)
        {
            if (officer.IsDead || officer.Faction == null || officer.Faction.ID != (int)GameFactions.IDs.ThePolice ||
                officer.Personality == null) return;
            IEnumerable<Actor> followers = officer.Followers;
            if (followers == null) return;
            foreach (Actor follower in new List<Actor>(followers))
            {
                NpcFact escort = officer.Personality.Knowledge.Facts.Find(f =>
                    f.Kind == "police_escort_started" && f.OtherId == follower.PersonalityIdentity &&
                    !officer.Personality.Knowledge.Facts.Exists(end =>
                        (end.Kind == "police_escort_finished" || end.Kind == "police_escort_ended") &&
                        end.ItemId == f.ItemId && end.EventTurn >= f.EventTurn));
                if (escort == null || officer.Location.Map.LocalTime.TurnCounter - escort.EventTurn < 2 * WorldTime.TURNS_PER_DAY &&
                    officer.Inventory.Items.Any(item => item.StoryIdentity == escort.ItemId && item.IsStolen)) continue;
                officer.RemoveFollower(follower);
                PersonalitySystem.Report(game, new SignificantEvent("police_escort_ended", officer, follower,
                    officer.Location.Map, officer.Location.Position, officer.Location.Map.LocalTime.TurnCounter,
                    causeId: escort.EventId, storyId: escort.StoryId) { ItemId = escort.ItemId });
            }
        }
    }
}
