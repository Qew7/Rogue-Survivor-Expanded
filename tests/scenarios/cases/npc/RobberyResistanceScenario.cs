using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberyResistanceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-resistance-rumor", () => TownScenarioFactory.Arena(9763,
            "..............................", "..............................", ".............................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor robber = NpcIntentSupport.Actor(world, "robber", 2, 1, "cruel", "selfish", "brave");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1, "brave", "hardened", "impulsive");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "lawful", "kind");
            Actor officer = PolicePursuitRumorScenario.Cop(world, "officer", 24, true);
            NpcIntentSupport.Player(world, 29, 2);
            ItemFood item = NpcIntentSupport.Food(world, victim, 1);
            witness.Personality.Opinion(robber.PersonalityIdentity, robber.UnmodifiedName);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            PersonalitySystem.Report(world.Game, new SignificantEvent("robbery_demanded", robber, victim,
                world.Map, robber.Location.Position, 0)
                { ItemId = item.StoryIdentity, ModelId = item.Model.ID, Resource = item.Model.SingleName, Units = item.Quantity });
            Check.Equal(true, witness.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_demanded" &&
                f.SubjectId == robber.PersonalityIdentity), "witness identifies the person making the threat");
            victim.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(victim);
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_resisted"),
                "courageous victim chooses armed resistance and performs an attack");
            Check.Equal(true, robber.IsAggressorOf(victim) && victim.IsSelfDefenceFrom(robber),
                "physical resistance is attributed to the robber's prior threat as self-defense");
            Check.Equal(false, witness.Personality.Knowledge.Facts.Any(f => f.Kind == "attack" &&
                f.OtherId == victim.PersonalityIdentity),
                "witness does not mistake the defensive blow for an unprovoked attack");
            Check.Equal(true, victim.Inventory.Contains(item), "resistance keeps the demanded item");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_resisted" &&
                f.SubjectId == victim.PersonalityIdentity && f.OtherId == robber.PersonalityIdentity),
                "witness distinguishes resistance from surrender");
            NpcFact demand = witness.Personality.Knowledge.Facts.Single(f => f.Kind == "robbery_demanded");
            NpcFact resistance = witness.Personality.Knowledge.Facts.Single(f => f.Kind == "robbery_resisted");
            world.Map.RemoveActor(witness); world.Place(witness, 23, 1);
            witness.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, officer, demand)),
                "witness tells the officer about the threat");
            witness.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, world.Try(new ActionNpcTell(witness, world.Game, officer, resistance)),
                "witness separately tells the officer about the victim's resistance");
            Check.Equal(true, officer.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_demanded" &&
                f.Source == NpcKnowledgeSource.Told), "officer retains the attempted robbery as hearsay");
            Check.Equal(true, officer.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_resisted" &&
                f.Source == NpcKnowledgeSource.Told), "officer retains the distinct response as hearsay");
            for (int i = 0; i < 15 && NpcIntentSupport.Intent(officer, "robbery_police_contact") == null; i++)
            { officer.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(officer); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.Intent(officer, "robbery_police_contact") != null,
                "police generates a separate intervention goal from a named robbery rumor");
            for (int i = 0; i < 55 && !NpcIntentSupport.HasEvent(officer, "robbery_police_confronted"); i++)
            { officer.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(officer); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(officer, "robbery_police_confronted"),
                "officer travels to the suspect and actually confronts them about the robbery");
            Check.Equal(false, NpcGoalGenerator.Evaluate(world.Game, officer).Any(g =>
                g.Capability.Id == "robbery_police_contact" && g.Target.Id == robber.PersonalityIdentity),
                "several reports about the same incident do not start repeated police confrontations");
        });
    }
}
