using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Generators;

static class RadioStudioScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/radio-studio", () => TownScenarioFactory.Arena(4637,
            "...", "...", "..."), world =>
        {
            BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
            parameters.MapWidth = 40;
            parameters.MapHeight = 40;
            parameters.GeneratePoliceStation = false;
            parameters.GenerateHospital = false;
            parameters.District = new District(new Point(0, 0), DistrictKind.BUSINESS);
            Map surface = new BaseTownGenerator(world.Game, parameters).Generate(4637);
            parameters.District.EntryMap = surface;
            Map studio = parameters.District.Maps.FirstOrDefault(m => m.Name == "Survivor Network studio");
            Check.Equal(true, studio != null, "business district has one underground station");
            Check.Equal(1, parameters.District.Maps.Count(m => m.Name == "Survivor Network studio"),
                "studio is unique");
            Actor host = studio.Actors.First(a => a.PersonalityIdentity == Session.Get.RadioHostId);
            Check.Equal(false, host.IsDead, "broadcaster starts alive");
            Check.Equal(true, studio.MapObjects.OfType<RadioReceiver>().Any(), "studio contains transmitter");
            Exit upstairs = studio.GetExitAt(1, 1);
            Check.Equal(surface, upstairs.ToMap, "studio has a surface exit");
            Check.Equal(studio, surface.GetExitAt(upstairs.ToPosition).ToMap, "surface stairs lead back");
            new BaseTownGenerator(world.Game, parameters).Generate(4638);
            Check.Equal(1, parameters.District.Maps.Count(m => m.Name == "Survivor Network studio"),
                "generating again does not duplicate the station");
        });
    }
}
