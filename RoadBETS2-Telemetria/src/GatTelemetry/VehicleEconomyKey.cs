using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace GatTelemetry;
internal static class VehicleEconomyKey
{
    private static string Normalize(string value){var b=new StringBuilder();foreach(char c in (value??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));return Regex.Replace(b.ToString(),"[^a-z0-9]+"," ").Trim();}
    public static string Resolve(string make,string model,string id,string type=""){
        string brand=Normalize(make),identity=Normalize(make+" "+model+" "+id);
        bool bus=Regex.IsMatch(Normalize(type),"^(bus|coach|onibus|autocarro)$")
          ||Regex.IsMatch(identity,@"\b(bus|coach|onibus|autocarro|marcopolo|busscar|comil|neobus|caio|irizar|paradiso|viaggio|torino|urbanuss)\b")
          ||Regex.IsMatch(identity,@"\b(vissta\s*buss|apache\s*vip|g[78])\b")
          ||Regex.IsMatch(identity,@"\bo\s?(371|400|500)[a-z0-9]*\b")
          ||Regex.IsMatch(identity,@"\bk\s?(230|250|270|310|320|340|360|380|400|410|420|440|450|460|480|500)[a-z0-9]*\b")
          ||Regex.IsMatch(identity,@"\bb\s?(7|9|10|11|12|13|250|270|290|340|360|380|420|450)(r|m|s|rh)[a-z0-9]*\b");
        return bus?"onibus":brand=="mercedes benz"?"mercedes":brand=="renault trucks"?"renault":brand;
    }
}
