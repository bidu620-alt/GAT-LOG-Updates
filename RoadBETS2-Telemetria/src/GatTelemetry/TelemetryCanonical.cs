using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

// Journal v2: representations are independent of JSON floating-point formatting.
internal static class TelemetryCanonical
{
    public static JObject ParseObject(string json)
    {
        using (var sr = new StringReader(json))
        using (var reader = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None })
            return JObject.Load(reader);
    }
    public static string Encode(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) return "n";
        if (token is JObject obj) {
            var properties = obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            return "o" + properties.Length.ToString(CultureInfo.InvariantCulture) + ":" + string.Concat(properties.Select(p => Encode(new JValue(p.Name)) + Encode(p.Value)));
        }
        if (token is JArray array) return "a" + array.Count.ToString(CultureInfo.InvariantCulture) + ":" + string.Concat(array.Select(Encode));
        if (token.Type == JTokenType.Boolean) return token.Value<bool>() ? "t" : "f";
        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) {
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Non-finite telemetry number");
            if (value == 0) value = 0; // Normalize negative zero, as JSON.parse/JSON.stringify do.
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return "d" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        if (token.Type == JTokenType.Date) {
            // Match the exact date string that Newtonsoft sends on the wire.
            using (var sr = new StringReader(token.ToString(Formatting.None)))
            using (var reader = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None })
                return Encode(JToken.ReadFrom(reader));
        }
        if (token.Type != JTokenType.String) throw new InvalidDataException("Unsupported telemetry token");
        byte[] text = Encoding.UTF8.GetBytes(token.Value<string>());
        return "s" + text.Length.ToString(CultureInfo.InvariantCulture) + ":" + Convert.ToBase64String(text);
    }
}
