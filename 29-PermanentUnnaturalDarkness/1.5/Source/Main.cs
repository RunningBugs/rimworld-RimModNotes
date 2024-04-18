using Verse;

namespace PermanentDarkness;

public class RainMonitorMapComponent : MapComponent
{

    public RainMonitorMapComponent(Map map) : base(map)
    {
    }

    public override void MapComponentTick()
    {
        if (GenTicks.IsTickInterval(300))
        {
            if (map.gameConditionManager.ConditionIsActive(GameConditionDefs.PermanentDarkness))
            {
                if (map.fireWatcher.LargeFireDangerPresent || !map.weatherManager.curWeather.temperatureRange.Includes(map.mapTemperature.OutdoorTemp))
                {
                    map.weatherManager.TransitionTo(WeatherDefs.Rain);
                }
            }
        }
    }
}