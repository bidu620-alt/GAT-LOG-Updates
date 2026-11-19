using System;
using Newtonsoft.Json.Linq;
using GatTelemetry;

class PassengerCheck
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        var bus = JObject.Parse(@"{'game':{'connected':true},'truck':{'make':'Volvo','model':'9700 DD','id':'volvo'},'job':{},'bus_job':{'active':true,'sourceCity':'A','destinationCity':'B','unknown_sdk_field':42}}");
        var before = bus["bus_job"].DeepClone();
        PassengerTelemetry.Stamp(bus);
        Check((string)bus["gat_scs_job_kind"] == "bus_job", "Active bus not captured");
        Check(JToken.DeepEquals(before,bus["bus_job"]), "Raw bus configuration changed");
        Check((string)bus.SelectToken("gat_passenger_capture.vehicle_model") == "9700 DD", "Model lost");
        Check(bus.SelectToken("gameplay.jobDelivered") == null, "Invented delivery");
        foreach (string model in new[]{"9700 DD", "Sprinter", "FH4"}) {
            var t = new JObject { ["truck"] = new JObject { ["make"]="Volvo", ["model"]=model } };
            PassengerTelemetry.Stamp(t);
            Check((string)t["gat_scs_job_kind"] == "job", "Model alone classified passengers");
        }
        var car = JObject.Parse(@"{'job':{'source_kind':'car_job'}}");
        PassengerTelemetry.Stamp(car);
        Check((string)car["gat_scs_job_kind"] == "car_job", "Car configuration lost");
        Check((string)car.SelectToken("gat_passenger_capture.status") == "passenger_service_not_confirmed", "Courier assumed passenger");
        var end = JObject.Parse(@"{'bus_job_event':'bus_job.delivered','bus_job':{'active':false}}");
        PassengerTelemetry.Stamp(end);
        Check((string)end["gat_scs_job_kind"] == "bus_job", "Terminal event lost");
        var ambiguous = JObject.Parse(@"{'job':{'source_kind':'job','active':true},'bus_job':{'active':true,'sourceCity':'A','destinationCity':'B'}}");
        PassengerTelemetry.Stamp(ambiguous);
        Check((string)ambiguous["gat_scs_job_kind"] == "job", "Ambiguous modes guessed");
        Console.WriteLine("PASS: passenger capture, model evidence, unknown attributes, courier, terminal and freight isolation.");
    }
}
