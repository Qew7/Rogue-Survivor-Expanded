using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;
sealed class WatchPatrolContent : INpcContentModule
{
    public string Id { get { return "scenario.watch-patrol"; } }
    public void Register(NpcCatalogBuilder c)
    {
        c.Fact("Surveyed");
        c.Event(new NpcEventDefinition("watch_order", NpcRecordCategory.Encounters, true,
            e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to scout a nearby area.",
            required: NpcEventFields.Subject | NpcEventFields.Other | NpcEventFields.Task));
        c.Event(new NpcEventDefinition("area_surveyed", NpcRecordCategory.Life, true,
            e => (e.Subject ?? "Someone") + " surveyed a nearby area."));
        c.Capability(new NpcIntentDefinition("survey_area", "Survey a proposed area", 90) {
            ResultFacts = (catalog, g) => catalog.Facts.Mask("Surveyed") });
        c.Operator(new NpcOperatorDefinition("scout.watch", null, exec => {
            var action = new NpcActionContext(exec);
            return action.Action(() => NpcPlanExecution.Near(exec.Game, exec.Owner, exec.Step.Place) &&
                NpcKnowledgeSystem.Visible(exec.Game, exec.Owner, exec.Step.Place), () => {
                exec.Game.DoWait(exec.Owner); action.Publish("area_surveyed"); action.Done(exec.Game.NpcContent.Facts.Mask("Surveyed"));
            });
        }));
        c.OperatorSource(new NpcOperatorSource("scout.watch", catalog => catalog.Facts.Mask("Surveyed"), domain => {
            ulong at = domain.At(domain.Goal.Destination); domain.Travel(domain.Goal.Destination, Guid.Empty, at);
            domain.Add("scout.watch", domain.Goal.Destination, Guid.Empty, at, 0,
                domain.Catalog.Facts.Mask("Surveyed"), default(NpcPlanningState), 1);
        }));
        c.Collective(new NpcCollectiveDefinition("group_watch", "watch_order", context => {
            NpcKnownPlace place = context.Knowledge.Places.Find(p => p.Kind == "lookout" && context.Turn - p.SeenTurn < 60);
            Actor follower = null;
            foreach (Actor actor in context.Visible) if (actor.Leader == context.Leader && NpcIntentSystem.Enabled(actor)) { follower = actor; break; }
            return place == null || follower == null ? null : new NpcCollectiveOffer(new NpcGroupPlan {
                Kind = "group_watch", Stage = "proposed", CollectorId = follower.PersonalityIdentity,
                BeneficiaryId = context.Leader.PersonalityIdentity, Destination = place.Place, Deadline = context.Turn + 90 }, 40);
        }, (a, b, p) => b.PersonalityIdentity == p.CollectorId, (a, p) => "Survey the nearby lookout.",
        (game, owner, source) => {
            if (source.Task.CollectorId != owner.PersonalityIdentity) return;
            NpcIntent goal = NpcStorySystem.StartKnown(game.NpcContent, owner, new NpcKnownPerson { Id = owner.PersonalityIdentity,
                Name = owner.UnmodifiedName, Place = owner.Location }, game.NpcContent.Capability("survey_area"), source.Id,
                source.StoryId, source.Task.Destination, owner.SocialGroup.Identity);
            if (goal != null) source.Task.Stage = "surveying";
        }));
        c.On("watch_order", NpcObservationPhase.Knowledge, o => NpcStorySystem.AcceptCollective(o.Game, o.Owner, o.Source));
    }
}
static class CollectiveExtensionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/collective-extension", () => TownScenarioFactory.Arena(4810,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            world.Game.NpcContent = PersonalityContent.Create(new WatchPatrolContent()).Content;
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal");
            Actor scout = NpcIntentSupport.Actor(world, "scout", 2, 1, "curious"); leader.AddFollower(scout);
            var visible = new List<Actor> { scout };
            Check.Equal(null, NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible), "unknown destination cannot create a collective task");
            Location destination = new Location(world.Map, new Point(4, 1));
            leader.Personality.Knowledge.RememberPlace(new NpcKnownPlace(destination, "lookout", 0));
            NpcGroupPlan plan = NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible);
            Check.Equal(true, plan != null && plan.Kind == "group_watch", "new content is proposed without a coordinator branch");
            Check.Equal(true, world.Try(new ActionNpcGroupPlan(leader, world.Game, scout, plan)), "leader communicates real task");
            NpcIntent goal = NpcIntentSupport.Intent(scout, "survey_area");
            Check.Equal(true, goal != null && goal.StoryId == plan.StoryId, "scout owns an independent goal");
            for (int t = 1; t < 10 && !goal.Finished; t++) { world.Map.LocalTime.TurnCounter = t; NpcIntentSupport.Turn(world, scout); }
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "new operator surveys only after actual travel");
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "area_surveyed"), "new event reaches the group chronicle");
            Check.Equal(false, new ActionNpcGroupPlan(leader, world.Game, scout, plan).IsLegal(), "finished proposal cannot repeat");
        });
    }
}
