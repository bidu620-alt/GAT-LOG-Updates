using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace GatTelemetry;
internal static class TruckBrandResolver {
 public static string Resolve(string make) {
 var builder=new StringBuilder();
 foreach(char c in (make??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)builder.Append(char.ToLowerInvariant(c));
 string n=Regex.Replace(builder.ToString(),"[^a-z0-9]+"," ").Trim();
 string found=null;
 if(n=="scania"||n.StartsWith("scania ",StringComparison.Ordinal)||n=="scania vabis"||n.StartsWith("scania vabis ",StringComparison.Ordinal)) { if(found!=null&&found!="scania")return n;found="scania"; }
 if(n=="volvo"||n.StartsWith("volvo ",StringComparison.Ordinal)||n=="volvo trucks"||n.StartsWith("volvo trucks ",StringComparison.Ordinal)) { if(found!=null&&found!="volvo")return n;found="volvo"; }
 if(n=="daf"||n.StartsWith("daf ",StringComparison.Ordinal)||n=="daf trucks"||n.StartsWith("daf trucks ",StringComparison.Ordinal)) { if(found!=null&&found!="daf")return n;found="daf"; }
 if(n=="iveco"||n.StartsWith("iveco ",StringComparison.Ordinal)) { if(found!=null&&found!="iveco")return n;found="iveco"; }
 if(n=="man"||n.StartsWith("man ",StringComparison.Ordinal)) { if(found!=null&&found!="man")return n;found="man"; }
 if(n=="mercedes benz"||n.StartsWith("mercedes benz ",StringComparison.Ordinal)||n=="mercedesbenz"||n.StartsWith("mercedesbenz ",StringComparison.Ordinal)||n=="mercedes"||n.StartsWith("mercedes ",StringComparison.Ordinal)||n=="mb"||n.StartsWith("mb ",StringComparison.Ordinal)) { if(found!=null&&found!="mercedes")return n;found="mercedes"; }
 if(n=="renault trucks"||n.StartsWith("renault trucks ",StringComparison.Ordinal)||n=="renault"||n.StartsWith("renault ",StringComparison.Ordinal)) { if(found!=null&&found!="renault")return n;found="renault"; }
 if(found==null)return n;
 if(found!="scania" && ((" "+n+" ").Contains(" scania ")||(" "+n+" ").Contains(" scania vabis ")))return n;
 if(found!="volvo" && ((" "+n+" ").Contains(" volvo ")||(" "+n+" ").Contains(" volvo trucks ")))return n;
 if(found!="daf" && ((" "+n+" ").Contains(" daf ")||(" "+n+" ").Contains(" daf trucks ")))return n;
 if(found!="iveco" && ((" "+n+" ").Contains(" iveco ")))return n;
 if(found!="man" && ((" "+n+" ").Contains(" man ")))return n;
 if(found!="mercedes" && ((" "+n+" ").Contains(" mercedes benz ")||(" "+n+" ").Contains(" mercedesbenz ")||(" "+n+" ").Contains(" mercedes ")||(" "+n+" ").Contains(" mb ")))return n;
 if(found!="renault" && ((" "+n+" ").Contains(" renault trucks ")||(" "+n+" ").Contains(" renault ")))return n;
 return found;
 }
}

