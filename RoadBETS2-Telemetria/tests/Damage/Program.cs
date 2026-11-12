using System;
using Newtonsoft.Json.Linq;
using GatTelemetry;
namespace GatTelemetry { internal static class ClientStore { public static void Log(string value) {} } }
class DamageCheck
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main()
    {
        var raw = JObject.Parse("{\"game\":{\"connected\":true},\"truck\":{\"wearEngine\":0.01,\"wearTransmission\":0.02,\"wearCabin\":0.03,\"wearChassis\":0.04,\"wearWheels\":0.05},\"trailers\":[{\"attached\":false,\"cargoDamage\":0.9},{\"attached\":true,\"cargoDamage\":0.006,\"wearBody\":0.007,\"wearChassis\":0.008,\"wearWheels\":0.009}]}");
        TelemetryEngine.Normalize(raw);
        string[] names = {"engine","transmission","cabin","chassis","wheels"};
        for(int i=0;i<5;i++) Check(Math.Abs(raw["truck_"+names[i]+"_damage_pct"].Value<double>()-(i+1))<0.00001,names[i]);
        Check(raw["truck_damage_pct"].Value<double>()==5,"truck maximum");
        Check(Math.Abs(raw["cargo_damage_pct"].Value<double>()-0.6)<0.00001,"attached cargo");
        Check(Math.Abs(raw["trailer_damage_pct"].Value<double>()-0.9)<0.00001,"attached trailer");
        var display=TelemetryEngine.BuildDisplay(raw);
        Check(display.EngineDamage=="1.0%" && display.WheelsDamage=="5.0%","display percentages");
        var absent=JObject.Parse("{\"truck\":{\"wearEngine\":null,\"wearTransmission\":null,\"wearCabin\":null,\"wearChassis\":null,\"wearWheels\":null}}");
        TelemetryEngine.Normalize(absent);
        Check(absent["truck_damage_pct"]==null,"absent data became zero");
        Check(TelemetryEngine.BuildDisplay(absent).EngineDamage=="—","missing damage must be visible");
        var zero=JObject.Parse("{\"truck\":{\"wearEngine\":0,\"wearTransmission\":0,\"wearCabin\":0,\"wearChassis\":0,\"wearWheels\":0}}");
        TelemetryEngine.Normalize(zero);
        Check(zero["truck_damage_pct"].Value<double>()==0,"real zero missing");
        Check(TelemetryEngine.BuildDisplay(zero).EngineDamage=="0.0%","zero display");
        Console.WriteLine("Damage integration checks passed: five truck components, attached trailer/cargo, percentages, null and real zero.");
    }
}
