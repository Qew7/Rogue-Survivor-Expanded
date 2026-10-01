using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryContinuationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-continuation", () => TownScenarioFactory.Arena(4671, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "lawful");
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1, "rebellious");
            owner.Personality.Knowledge.See(target, 0);
            NpcKnownPerson belief = owner.Personality.Knowledge.Person(target.PersonalityIdentity);
            belief.Violation = belief.ViolationConfidence = 100;
            NpcIntentSupport.Turn(world, owner); NpcIntent first = NpcIntentSupport.Intent(owner, "confront_reported_aggressor");
            Check.Equal(true, Session.Get.NpcDirector.Find(first.StoryId).Finished, "initial communication can finish its own episode");
            NpcIntentSupport.Turn(world, target);
            NpcFact reply = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "boundary_defied");
            Check.Equal(first.StoryId, reply.StoryId, "actual answer preserves the episode that prompted it");
            world.Map.LocalTime.TurnCounter = 181; NpcGoalGenerator.Refresh(world.Game, owner, true);
            NpcIntent next = null; foreach (NpcIntent intent in owner.Personality.Intents) if (!intent.Finished && intent.Generated.Value == NpcGoalValue.Justice) next = intent;
            Check.Equal(true, next != null && next.StoryId != first.StoryId, "a persistent consequence produces an independent later goal");
            Check.Equal(true, Session.Get.NpcDirector.Find(next.StoryId).Parents.Contains(first.StoryId), "causal episode link survives completion of the predecessor");
            Session.Get.NpcDirector.Link(Session.Get.NpcDirector.Find(first.StoryId), next.StoryId, owner, reply.EventId);
            Check.Equal(false, Session.Get.NpcDirector.Find(first.StoryId).Parents.Contains(next.StoryId), "a reverse link cannot create a cycle");
            Check.Equal(reply.EventId, next.CauseId, "new action is motivated by the real refusal");
            Check.Equal(true, next.Generated.Causes.Length > 0, "supporting evidence is retained with the goal");
            Session.Get.WorldTime.TurnCounter = 181;
            string text = String.Join(" ", RecordsReader.Lines(new RecordsSave("test", Session.Get), null));
            Check.Equal(true, text.Contains("Continuation of"), "chronicle connects the independently generated episodes");
        });
    }
}
