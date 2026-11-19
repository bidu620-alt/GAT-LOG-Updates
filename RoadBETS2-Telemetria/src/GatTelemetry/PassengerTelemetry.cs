using System;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

// Read only the current provider sample. Never infer passenger work from a brand
// purchase, model name or a previous packet. Stamp before the journal is sealed.
internal static class PassengerTelemetry
{
    private static string Text(JObject raw, params string[] paths)
    {
        foreach (var path in paths) {
            var value = raw.SelectToken(path, false);
            if (value?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value))
                return ((string)value).Trim();
        }
        return "";
    }

    public static void Stamp(JObject raw)
    {
        if (raw == null) return;
        string kind = Text(raw, "job.source_kind", "job.sourceKind", "gat_scs_job_kind", "scs_configuration_id");
        string sdkEvent = Text(raw, "gat_scs_event", "bus_job_event", "gameplay.busJobEvent");
        var bus = raw["bus_job"] as JObject;
        bool activeBus = (bus?["active"]?.Type == JTokenType.Boolean && (bool)bus["active"]) ||
            (bus?["onJob"]?.Type == JTokenType.Boolean && (bool)bus["onJob"]);
        bool hasRoute = Text(raw, "bus_job.sourceCity", "bus_job.source_city").Length > 0 &&
            Text(raw, "bus_job.destinationCity", "bus_job.destination_city").Length > 0;
        bool freightActive = raw.SelectToken("job.active")?.Value<bool?>() == true ||
            raw.SelectToken("job.onJob")?.Value<bool?>() == true ||
            (Text(raw, "job.sourceCity", "job.source_city").Length > 0 &&
             Text(raw, "job.destinationCity", "job.destination_city").Length > 0);
        if (sdkEvent == "bus_job.delivered" || sdkEvent == "bus_job.cancelled" ||
            (activeBus && hasRoute && !freightActive && (kind == "" || kind == "job"))) kind = "bus_job";
        if (kind != "bus_job" && kind != "car_job" && kind != "job") kind = "job";
        raw["gat_scs_job_kind"] = kind;
        raw["gat_passenger_capture"] = new JObject {
            ["schema"] = 1,
            ["configuration"] = kind,
            ["bus_configuration_present"] = bus != null && bus.HasValues,
            ["status"] = kind == "bus_job" ? "bus_job_received" : "passenger_service_not_confirmed",
            ["vehicle_make"] = Text(raw, "truck.make", "truck.manufacturer", "truck.brand"),
            ["vehicle_model"] = Text(raw, "truck.model", "truck.modelName", "truck.model_name"),
            ["vehicle_id"] = Text(raw, "truck.id", "truck.unitId", "truck.vehicleId"),
            ["sdk_event"] = sdkEvent
        };
        // bus_job and provider-specific attributes remain intact in the packet.
        // car_job is deliberately not assumed to mean passengers (courier jobs exist).
    }
}
