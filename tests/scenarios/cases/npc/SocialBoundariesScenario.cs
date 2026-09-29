using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class SocialBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/social-boundaries", () => TownScenarioFactory.Arena(4670, "...#.....", "...#.....", "...#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 2, 2);
            Actor carer = NpcIntentSupport.Actor(world, "carer", 1, 1, "kind");
            Actor hidden = NpcIntentSupport.Actor(world, "hidden", 5, 1); hidden.HitPoints = 1;
            carer.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            NpcIntentSupport.Turn(world, carer);
            Check.Equal(null, NpcIntentSupport.Intent(carer, "medical_aid"), "hidden real injuries are not inspected");
            Check.Equal(1, hidden.HitPoints, "unseen target receives no imaginary treatment");
            carer.Personality.Knowledge.See(hidden, 0);
            NpcIntent goal = NpcStorySystem.StartKnown(carer, carer.Personality.Knowledge.Person(hidden.PersonalityIdentity), NpcIntentContent.MedicalAid);
            var step = new NpcPlanStep { Action = NpcPlanAction.TreatPerson, Target = hidden.PersonalityIdentity, Place = hidden.Location };
            goal.Plan = new NpcPlan(); goal.Plan.Steps.Add(step);
            var action = new ActionNpcAid(carer, world.Game, goal, step, hidden);
            Check.Equal(false, action.IsLegal(), "remembering a person does not permit remote treatment"); action.Perform();
            Check.Equal(1, carer.Inventory.CountItems, "invalid aid consumes no medicine");
            Check.Equal(false, NpcIntentSupport.HasEvent(carer, "treated_person"), "invalid aid emits no consequence");
            Session.Get.GamePreset.NpcPersonalitiesEnabled = false;
            NpcPromises.Expire(carer); NpcGoalGenerator.Refresh(world.Game, carer);
            Check.Equal(false, action.IsLegal(), "disabled personality preset prevents social actions");
            Session.Get.GamePreset.NpcPersonalitiesEnabled = true;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            for (int i = 0; i < 16; i++) listener.Personality.RememberCommitment(new NpcCommitment { Id = 1000 + i,
                Promisor = Guid.NewGuid(), Beneficiary = listener.PersonalityIdentity, DueTurn = 180 });
            var reply = new NpcReaction(listener, "I'll bring food.", 99, 0, "food_promised", null);
            carer.Personality.Reactions.Add(reply);
            var promiseAction = new ActionNpcReaction(carer, world.Game, reply, listener);
            Check.Equal(false, promiseAction.IsLegal(), "full active obligation history prevents an unrecordable promise");
            promiseAction.Perform();
            Check.Equal(16, listener.Personality.Commitments.Count, "active obligations are not discarded to accept another");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "food_promised"), "rejected promise emits no event");
            listener.Personality.Commitments[0].Status = NpcCommitmentStatus.Kept;
            Check.Equal(true, promiseAction.IsLegal(), "completed history makes room for a new promise");
        });
    }
}
