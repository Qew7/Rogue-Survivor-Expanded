using System;
using System.Collections.Generic;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

static class AllRumorIdentityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/all-rumor-identities", () => TownScenarioFactory.Arena(4909,
            "...", "...", "..."), world =>
        {
            var definitions = (Dictionary<string, NpcEventDefinition>)typeof(NpcContentCatalog)
                .GetField("events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Game.NpcContent);
            int checkedCount = 0;
            foreach (NpcEventDefinition definition in definitions.Values)
            {
                if (!definition.RetainFact) continue;
                var fact = new NpcFact { Kind = definition.Id, SubjectName = "Secret Subject", OtherName = "Secret Other",
                    SubjectReportName = "a biker", OtherReportName = "a police officer",
                    SubjectId = Guid.NewGuid(), OtherId = Guid.NewGuid(), Place = new Location(world.Map, new System.Drawing.Point(1, 1)),
                    Resource = "food", Units = 2 };
                string report = NpcRecordDescriptions.Report(world.Game.NpcContent, fact);
                Check.Equal(false, report.Contains("Secret Subject") || report.Contains("Secret Other"),
                    definition.Id + ": rumor never reveals hidden actor names");
                if (definition.Describe != null)
                {
                    string archive = definition.Describe(new ObservedEvent(definition.Id, 0,
                        fact.SubjectName, fact.OtherName, false, subjectId: fact.SubjectId, otherId: fact.OtherId));
                    if (archive.Contains("Secret Subject") && definition.Id != "base_theft")
                        Check.Equal(true, report.Contains("a biker"), definition.Id + ": report uses the subject's faction description");
                    if (definition.Id == "base_theft")
                        Check.Equal(false, report.Contains("a biker"), "unidentified base thief stays anonymous");
                    if (archive.Contains("Secret Other"))
                        Check.Equal(true, report.Contains("a police officer"), definition.Id + ": report uses the other actor's faction description");
                }
                checkedCount++;
            }
            Check.Equal(true, checkedCount > 20, "all registered reportable event kinds are checked");
        });
    }
}
