using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class RobberySurrenderScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/robbery-surrender", () => TownScenarioFactory.Arena(9761,
            "..............................", "..............................", ".............................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor robber = NpcIntentSupport.Actor(world, "robber", 1, 1, "cruel", "selfish", "brave");
            Check.Equal(world.Game.GameFactions.TheCivilians.ID, robber.Faction.ID,
                "robbery candidate is an ordinary civilian, not a gang member");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 1, "timid", "kind");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1, "lawful", "kind");
            Actor officer = PolicePursuitRumorScenario.Cop(world, "officer", 5, true);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 24, 1, "lawful", "kind");
            NpcIntentSupport.Player(world, 29, 2);
            ItemFood food = NpcIntentSupport.Food(world, victim, 1);
            witness.Personality.Opinion(robber.PersonalityIdentity, robber.UnmodifiedName);
            witness.Personality.Opinion(victim.PersonalityIdentity, victim.UnmodifiedName);
            Guid itemId = food.StoryIdentity;
            for (int i = 0; i < 15 && !NpcIntentSupport.HasEvent(robber, "robbery_demanded"); i++)
            { robber.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(robber); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(robber, "robbery_demanded"),
                "eligible robber chooses and speaks a demand for the actual carried item");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "robbery_demanded"),
                "visible witness retains the attempted robbery");
            Check.Equal(true, victim.Inventory.Contains(food), "the threat alone does not transfer ownership");
            for (int i = 0; i < 12 && !NpcIntentSupport.HasEvent(victim, "robbery_dropped"); i++)
            { victim.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(victim); world.Map.LocalTime.TurnCounter++; }
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_accepted"),
                "timid victim accepts the threat through a social reply");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "robbery_accepted"),
                "witness observes the victim's agreement separately from the threat");
            Check.Equal(true, NpcIntentSupport.HasEvent(victim, "robbery_dropped"),
                "victim actually spends a turn dropping the item");
            Check.Equal(false, victim.Inventory.Contains(food), "surrender removes the item from the victim's inventory");
            Check.Equal(true, world.Map.GetItemsAt(victim.Location.Position) != null &&
                world.Map.GetItemsAt(victim.Location.Position).Items.Any(it => it.StoryIdentity == itemId),
                "surrendered item is physically on the ground");
            for (int i = 0; i < 18 && !NpcIntentSupport.HasEvent(robber, "robbery_taken"); i++)
            {
                victim.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(victim);
                robber.ActionPoints = Rules.BASE_ACTION_COST; world.NpcTurn(robber);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(robber, "robbery_taken"),
                "robber follows the drop and really picks up the item");
            Check.Equal(true, robber.Inventory.Items.Any(it => it.StoryIdentity == itemId && it.IsStolen),
                "taken item retains the victim and crime provenance");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "robbery_taken"),
                "witness distinguishes successful robbery from a mere threat");
            Check.Equal(true, officer.Personality.Knowledge.Facts.Any(f => f.Kind == "robbery_taken" &&
                f.SubjectId == robber.PersonalityIdentity), "officer recognizes the completed theft");
            Check.Equal(true, NpcGoalGenerator.Evaluate(world.Game, officer).Any(candidate =>
                candidate.Capability.Id == "police_contact_thief" &&
                candidate.State.ObligationId == officer.Personality.Knowledge.Facts.Find(f => f.Kind == "robbery_taken").EventId),
                "police recovery system offers a restitution investigation for stolen robbery goods");
            Check.Equal(true, NpcIntentSupport.HasEvent(witness, "robbery_dropped"),
                "witness observes the physical surrender separately from the robbery");
            world.Map.RemoveActor(witness); world.Place(witness, 23, 1);
            foreach (string kind in new[] { "robbery_accepted", "robbery_dropped", "robbery_taken" })
            {
                NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == kind);
                Check.Equal(true, fact != null, "witness can retell " + kind);
                if (!listener.Personality.Knowledge.Facts.Any(f => f.Kind == kind))
                {
                    witness.ActionPoints = Rules.BASE_ACTION_COST;
                    Check.Equal(true, world.Try(new djack.RogueSurvivor.Engine.Actions.ActionNpcTell(witness, world.Game, listener, fact)),
                        "ordinary rumor speech carries " + kind);
                }
                Check.Equal(true, listener.Personality.Knowledge.Facts.Any(f => f.Kind == kind &&
                    f.Source == NpcKnowledgeSource.Told), "listener distinguishes heard " + kind);
            }
            Check.Equal(true, listener.Personality.Person(robber.PersonalityIdentity) != null &&
                listener.Personality.Person(robber.PersonalityIdentity).Grievance > 0,
                "listener judges the robber after hearing the completed crime");
            string save = Path.Combine(Path.GetTempPath(), "robbery-surrender-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(save, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(save); loaded.ReconstructAuxiliaryFields();
                Actor savedRobber = NpcIntentSupport.Find(loaded, robber.PersonalityIdentity);
                Check.Equal(true, savedRobber.Inventory.Items.Any(it => it.StoryIdentity == itemId && it.IsStolen && it.TheftEventId > 0),
                    "robbery provenance survives save and load");
            }
            finally
            {
                if (File.Exists(save)) File.Delete(save);
                if (File.Exists(save + ".bak")) File.Delete(save + ".bak");
            }
        });
    }
}
