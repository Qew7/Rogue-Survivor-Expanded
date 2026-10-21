using System;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class SocialGraphExportScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/social-graph-export", () => TownScenarioFactory.Arena(9471,
            ".....", ".....", "....."), world =>
        {
            Session live = Session.Get;
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor giver = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor receiver = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            giver.Personality = new PersonalityState();
            receiver.Personality = new PersonalityState();
            world.Place(giver, 1, 1); world.Place(receiver, 2, 1);
            receiver.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            giver.Inventory.AddAll(food);
            world.Game.DoGiveItemTo(giver, receiver, food);
            receiver.Personality.ChangeExperience(NpcPersonalityAxis.Patience, 12);
            Session.Get.ResidentRecords.CaptureAxes(world.Map, world.Map.LocalTime.TurnCounter,
                world.Game.NpcContent);
            string path = Path.Combine(Path.GetTempPath(), "social-graph-" + Guid.NewGuid().ToString("N") + ".dat");
            string json = path + ".json";
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                SocialGraphDocument graph = SocialGraphExport.Build(RecordsReader.Load(path));
                Check.Equal(2, graph.residents.Count, "two saved NPCs are available for graph navigation");
                Check.Equal(2, graph.residents.Select(p => p.id).Distinct().Count(),
                    "namesakes retain separate stable identities");
                SocialGraphResident recipient = graph.residents.Single(p => p.id == receiver.PersonalityIdentity.ToString("D"));
                RecordsSave archive = RecordsReader.Load(path);
                Check.Equal(archive.Profiles.Single(p => p.Resident.Identity == receiver.PersonalityIdentity).Score,
                    recipient.interestScore, "atlas interest score matches Read Records after save/load");
                Check.Equal(true, recipient.entries.Any(e => e.kind == "helped" &&
                    (e.subject == giver.PersonalityIdentity.ToString("D") ||
                     e.other == giver.PersonalityIdentity.ToString("D"))),
                    "saved joint event links the actual participants");
                Check.Equal(true, recipient.entries.Any(e => e.kind == "helped" &&
                    e.axesAfter != null && e.axesAfter.Length == 13),
                    "end-of-turn axis snapshot survives archive-only save/load");
                Check.Equal(PersonalitySystem.Disposition(receiver, NpcPersonalityAxis.Patience,
                    world.Game.NpcContent), recipient.entries.First(e => e.kind == "helped").axesAfter[12],
                    "saved Patience axis is the effective value after the turn");
                Check.Equal(true, graph.nodes.Any(n => n.id == "event:helped"),
                    "event definitions come from the active catalog");
                Check.Equal(true, graph.edges.Any(e => e.from == "event:helped" &&
                    e.to.StartsWith("handler:helped:", StringComparison.Ordinal)),
                    "registered event handlers appear in the graph");
                Check.Equal(true, graph.edges.Any(e => e.label == "Слух"),
                    "registered rumor handlers appear in the graph");
                Check.Equal(true, graph.nodes.Where(n => n.type == "handler").All(n =>
                    !n.label.Contains("b__") && !n.detail.Contains("b__") && !n.label.Contains("<>")),
                    "compiler-generated method names are not shown as social rules");
                Check.Equal(true, graph.nodes.Any(n => n.id == "handler:shared_food:after-event:0" &&
                    n.label == "После события: shared food · PromisesModule" && n.detail.Contains("PromisesModule")),
                    "promise completion has a readable event, phase and module label");
                Check.Equal(true, graph.edges.Any(e => e.from == "handler:shared_food:after-event:0" &&
                    e.to == "flow:after-event"),
                    "completion handler is linked to its execution phase, not flagged as a dead end");
                Check.Equal(true, graph.nodes.Where(n => n.type == "handler").All(n =>
                    graph.edges.Any(e => e.from == n.id && e.to.StartsWith("flow:", StringComparison.Ordinal))),
                    "every handler identifies its execution phase");
                Check.Equal(true, graph.edges.Any(e => e.from.StartsWith("memory:", StringComparison.Ordinal) &&
                    e.to.StartsWith("axis:", StringComparison.Ordinal)),
                    "memory disposition outcomes reach axes in the rules graph");
                SocialGraphExport.Write(path, json);
                string exported = File.ReadAllText(json);
                Check.Equal(true, exported.Contains(receiver.PersonalityIdentity.ToString("D")),
                    "JSON export contains saved actor identity");
                Check.Equal(false, exported.Contains("window.SOCIAL_GRAPH_DATA"),
                    "save export is plain JSON for the file picker");
                Check.Same(live, Session.Get, "export does not replace the active session");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(json)) File.Delete(json);
            }
        });
    }
}
