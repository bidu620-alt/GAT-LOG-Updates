using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

// Never fills identity from a previous sample or from a driver's selected truck.
internal sealed class TruckModelEvidence
{
    public string Make = "", Model = "", RawId = "", Signature = "";
    public bool Connected, Paused, Identified;
    public static string Text(JObject raw, params string[] paths)
    {
        foreach (string path in paths) {
            var token = raw?.SelectToken(path, false);
            if (token != null && token.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)token))
                return ((string)token).Trim().Substring(0, Math.Min(160, ((string)token).Trim().Length));
        }
        return "";
    }
    public static string Normalize(string text)
    {
        var b = new StringBuilder();
        foreach (char c in (text ?? "").Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) b.Append(char.ToLowerInvariant(c));
        return Regex.Replace(b.ToString(), "[^a-z0-9]+", " ").Trim();
    }
    public static string BrandKey(string make) { string key = Normalize(make); return key == "mercedes benz" ? "mercedes" : key == "renault trucks" ? "renault" : key; }
    public static TruckModelEvidence Read(JObject raw)
    {
        var e = new TruckModelEvidence();
        e.Connected = raw?.SelectToken("game.connected")?.Value<bool?>() == true;
        e.Paused = raw?.SelectToken("game.paused")?.Value<bool?>() == true;
        e.Make = Text(raw, "truck.make", "truck.manufacturer", "truck.brand", "truck.makeName");
        e.Model = Text(raw, "truck.model", "truck.modelName", "truck.model_name");
        e.RawId = Text(raw, "truck.id", "truck.unitId", "truck.unit_id", "truck.vehicleId");
        e.Identified = e.Connected && Normalize(e.Make).Length > 0;
        e.Signature = e.Identified ? "brand|" + BrandKey(e.Make) : "";
        return e;
    }
    public static void Stamp(JObject raw)
    {
        if (raw == null) return;
        var e = Read(raw);
        raw["gat_truck_evidence"] = new JObject {
            ["schema"] = 1, ["make"] = e.Make, ["model"] = e.Model, ["raw_id"] = e.RawId,
            ["signature"] = e.Signature, ["identified"] = e.Identified,
            ["source"] = "trucksim_gps_current_sample", ["observed_at"] = DateTime.UtcNow.ToString("o")
        };
    }
}
