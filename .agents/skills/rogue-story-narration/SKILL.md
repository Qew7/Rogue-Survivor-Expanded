---
name: rogue-story-narration
description: Add or improve NPC rumor and radio-story narration in Rogue Survivor, including new reportable events and their causal links.
---

# Rumors as stories

The source event becomes a retained `NpcFact` in `NpcKnowledgeSystem.RetainEvent`; `NpcFact.Retell` carries it to other listeners. `NpcConversation.Chapter` selects an ordered, bounded group of facts, `ReportSentence` renders each fact, and `StoryText` joins the visible reports. Conversations call it from `ShareRumor`; radio calls it from `RogueGame.Radio.cs`. Keep both paths consistent.

For a new reportable event, register a truthful `NpcEventDefinition` report description in its owning content module. Give related events the same `StoryId`. Set `SignificantEvent.CauseId` to a real earlier event ID only when the action actually has that link; carry it through any manually created fact. Retelling must preserve the event ID, cause ID, time, place, source and report names. Missing or old-save cause IDs mean no causal wording.

Compose only from facts the speaker knows. Preserve the source's uncertainty and identity boundaries; do not turn hearsay into an eyewitness account, infer a person's name, or invent motives. Use causal connectors only for an explicit link to the immediately preceding visible fact. Use chronological or neutral wording otherwise. Avoid repeating the source lead and location when their meaning is unchanged. Collapse duplicate visible sentences without dropping underlying facts: all eligible listeners and radio recipients must still learn each distinct event.

For repeated actions, put an optional `NpcEventDefinition.SummarizeReports` callback in the event's owning module. The composer groups only consecutive reports with the same known subject, distinct known recipients, matching place and evidence lead, and no causal link being hidden. The callback returns a factual clause without an evidence lead or place. Set `ReportConclusion` only on a known outcome, such as a kept promise; it changes the final transition, not the event description. Set `ReportDisputesKinds` only for a recorded contradiction sharing the original event ID. Keep uncertain claims explicitly framed as claims. `npc/story-request-grouping` and `npc/story-disputed-accounts` cover these cases.

Radio keeps the complete `RadioProgram.Facts` for knowledge and sanity effects. For a familiar `StoryId`, its text includes only event ID/kind pairs the player has not learned; an unchanged story gets a short update instead of a replay. Station leads live in `RogueGame.Radio.cs`; change the framing without changing the fact's truth or evidence source. `npc/radio-story-continuation` checks all four stations and the player's journal.

Add a named deterministic scenario under `tests/scenarios/cases/npc/` that exercises the real event-to-fact-to-speech path and, when relevant, radio. Check an unrelated or missing-cause boundary as well as a linked chain. If persisted fact fields change, update `docs/save-format.md` and test save/load and another retelling. Run `sh tests/scenario.sh <name>` and `docker build --progress=plain --target test .` using `rogue-docker`. Run e2e only for UI, input, rendering, startup or asset changes. Do not restart a running game container unless the user asks.
