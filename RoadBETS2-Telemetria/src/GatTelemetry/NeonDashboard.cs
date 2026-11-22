using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;
internal sealed partial class MainForm
{
    private void ApplyNeonDashboard(JObject d)
    {
        var route=d["route"] as JObject??new JObject();
        var cargo=d["cargo"] as JObject??new JObject();
        var fuel=d["fuel"] as JObject??new JObject();
        var truck=d["truck"] as JObject??new JObject();
        var info=d["display"] as JObject??new JObject();
        bool online=info["connected"]?.Value<bool?>()==true;
        bool active=online&&info["on_job"]?.Value<bool?>()==true;
        SetRoadLifeText("SourceText", active?RoadLifeString(route["source"],"—"):"—");
        SetRoadLifeText("DestinationText", active?RoadLifeString(route["destination"],"—"):"—");
        SetRoadLifeText("TripStatusText", online?(active?"EM ROTA":"SEM VIAGEM ATIVA"):"GPS DESCONECTADO");
        SetRoadLifeText("CargoStatusText", active?"CARGA EM TRANSPORTE":"SEM CARGA ATIVA");
        SetRoadLifeText("FuelText", online?RoadLifeInt(RoadLifeDouble(fuel["current_l"]))+" L":"—");
        SetRoadLifeText("FuelCapacityText", online?"DE "+RoadLifeInt(RoadLifeDouble(fuel["capacity_l"]))+" L":"CAPACIDADE —");
        SetRoadLifeText("CargoWeight", active?RoadLifeDec(RoadLifeDouble(cargo["weight_t"]),1)+" t":"—");
        double range=RoadLifeDouble(info["fuel_range_km"]);
        bool cruiseOn=online&&RoadLifeBool(truck["cruise"]);
        double cruiseSpeed=RoadLifeDouble(truck["cruise_speed_kmh"]);
        SetRoadLifeText("CruiseText",!online?"—":cruiseOn&&cruiseSpeed>0?RoadLifeInt(cruiseSpeed)+" km/h":"DESLIGADO");
        if(_roadLifeOverlay.FindName("CruiseText") is TextBlock cruiseLabel){
            var color=cruiseOn?Color.FromRgb(0,215,156):Color.FromRgb(170,190,181);
            if(!(cruiseLabel.Foreground is SolidColorBrush brush)||brush.Color!=color)cruiseLabel.Foreground=new SolidColorBrush(color);
        }
        SetRoadLifeText("RangeText", online&&!double.IsNaN(range)?"AUTONOMIA  "+RoadLifeInt(range)+" km":"AUTONOMIA  —");
        SetRoadLifeText("TrailerInfoText", RoadLifeString(info["trailer_model"],info["trailer_connected"]?.Value<bool?>()==true?"Reboque conectado":"Reboque não identificado"));
        SetRoadLifeText("CargoRightName", active?RoadLifeString(cargo["name"],"Carga identificada").ToUpperInvariant():"AGUARDANDO VIAGEM");
        if(_roadLifeOverlay.FindName("SpeedNeedleRotation") is RotateTransform needle)
            needle.Angle=-135+Math.Max(0,Math.Min(160,RoadLifeDouble(truck["speed_kmh"])))*270/160;
        if(!active&&_roadLifeOverlay.FindName("CargoPreview") is Image oldImage)oldImage.Visibility=Visibility.Collapsed;
        if(_roadLifeOverlay.FindName("CargoEmptyIcon") is FrameworkElement empty)
            empty.Visibility=(_roadLifeOverlay.FindName("CargoPreview") is Image image&&image.Visibility==Visibility.Visible)?Visibility.Collapsed:Visibility.Visible;
        if(!online){foreach(string name in new[]{"SpeedText","GearText","RemainBottom","TravelBottom","TruckText","TruckDamageText","TrailerDamageText","CargoDamageText","LimitText"})SetRoadLifeText(name,"—");}
    }
}
