using System;using System.IO;using System.Drawing;using System.Linq;using Newtonsoft.Json.Linq;using Forms=System.Windows.Forms;
namespace GatTelemetry;
internal sealed partial class MainForm {
 private PassengerJourney _passengerJourney;private JObject _passengerLastRaw;private DateTime _passengerLastAt;private bool _passengerServerReady;private double _passengerMinimumKm=200;private Forms.Button _passengerButton;private string _passengerLoadError="";
 private void ObservePassengerVehicle(JObject raw){if(raw==null)return;_passengerLastRaw=(JObject)raw.DeepClone();_passengerLastAt=DateTime.UtcNow;}
 private JObject PassengerDashboardView(JObject raw){if(raw==null||_passengerJourney==null||!_passengerJourney.Matches(raw,_accountUser))return raw;var view=(JObject)raw.DeepClone();var current=_passengerJourney.Current;view["cargo_name"]="Passageiros";view["source_city"]=current["origin"];view["destination_city"]=current["destination"];view["on_job"]=true;view["passenger_distance_km"]=(double?)current["last_server_distance_km"]??0;return view;}
 private PassengerJourney PassengerStore(){if(_passengerJourney==null&&_passengerLoadError==""){try{_passengerJourney=new PassengerJourney(Path.Combine(ClientStore.DataDir,"passenger-journey.dat"));}catch(Exception ex){_passengerLoadError="Não foi possível recuperar a viagem de passageiros. O arquivo foi preservado.";ClientStore.Log("Passenger recovery: "+ex.GetType().Name);}}return _passengerJourney;}
 private Forms.Button CreatePassengerButton(){_passengerButton=new Forms.Button{Text="PASSAGEIROS",Dock=Forms.DockStyle.Bottom,Height=35,FlatStyle=Forms.FlatStyle.Flat,BackColor=Color.FromArgb(8,68,38),ForeColor=Color.White};_passengerButton.Click+=(s,e)=>OpenPassengerDialog();return _passengerButton;}
 private void StampPassengerJourney(JObject raw){
  if(raw==null||raw["gat_journal_chain"]!=null)return;
  if(_passengerLastRaw==null)ObservePassengerVehicle(raw);var store=PassengerStore();if(store==null)return;
  store.ObserveInactiveService(raw);
  var service=PassengerService.Read(raw,DateTime.UtcNow);
  if(_passengerServerReady&&AccountReady&&store.Current==null&&service!=null&&store.CanAutoStart(service)&&(string)VehicleClassification.Read(raw)["vehicle_type"]=="bus"){
   try{store.Start(raw,_accountUser,(string)service["origin"],(string)service["destination"],(string)service["source"],(string)service["service_id"]);}catch(Exception ex){ClientStore.Log("Passenger auto-start: "+ex.Message);}
  }
  var current=store.Current;string sdkEvent=(string)raw["gat_scs_event"]??(string)raw["bus_job_event"]??(string)raw.SelectToken("gameplay.busJobEvent")??"";
  if(current!=null&&(string)current["source"]=="sdk"&&(string)current["action"]=="progress"){
   if(sdkEvent=="bus_job.delivered")store.Command("finish");else if(sdkEvent=="bus_job.cancelled")store.Command("cancel");
  }
  store.Apply(raw,_accountUser);
  if(_passengerButton!=null){bool bus=(string)raw["vehicle_type"]=="bus";_passengerButton.Enabled=AccountReady&&_passengerServerReady&&(bus||store.Current!=null);_passengerButton.Text=store.Current==null?(bus?"INICIAR PASSAGEIROS":"PASSAGEIROS • AGUARDANDO ÔNIBUS"):"PASSAGEIROS • VER VIAGEM";}
 }
 private void AcknowledgePassengers(JObject response){_passengerServerReady=response?.Value<int?>("passenger_capabilities")==1;if(response?.Value<double?>("passenger_min_km") is double minimum && minimum>=0)_passengerMinimumKm=minimum;PassengerStore()?.Acknowledge(response);}
 private void OpenPassengerDialog(){
  if(!string.IsNullOrWhiteSpace(_passengerLoadError)){Forms.MessageBox.Show(this,_passengerLoadError,"Passageiros");return;}
  if(!_passengerServerReady){Forms.MessageBox.Show(this,"Aguardando conexão com a Central atualizada para passageiros.","Passageiros");return;}
  var store=PassengerStore();if(store==null)return;var current=store.Current;
  using(var dialog=new Forms.Form{Text="Transporte de passageiros",Size=new Size(490,340),StartPosition=Forms.FormStartPosition.CenterParent,FormBorderStyle=Forms.FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(15,27,22),ForeColor=Color.White}){
   var origin=new Forms.TextBox{Left=18,Top=52,Width=435,MaxLength=120,Text=(string)current?["origin"]??"",ReadOnly=current!=null};var destination=new Forms.TextBox{Left=18,Top=108,Width=435,MaxLength=120,Text=(string)current?["destination"]??"",ReadOnly=current!=null};
   dialog.Controls.Add(new Forms.Label{Left=18,Top=26,Width=430,Text="Origem"});dialog.Controls.Add(origin);dialog.Controls.Add(new Forms.Label{Left=18,Top=82,Width=430,Text="Destino"});dialog.Controls.Add(destination);
   var confirmed=new Forms.CheckBox{Left=18,Top=145,Width=435,Height=38,Text="Confirmo que estou realizando um serviço real de passageiros.",Checked=current!=null,Enabled=current==null};dialog.Controls.Add(confirmed);
   dialog.Controls.Add(new Forms.Label{Left=18,Top=188,Width=435,Height=47,Text=string.IsNullOrWhiteSpace(store.Message)?"Mínimo: "+_passengerMinimumKm.ToString("0")+" km. XP, Gold, danos, velocidade e chave de marca seguem as regras atuais da Central.":PassengerMessage(store.Message)});
   var start=new Forms.Button{Left=18,Top=244,Width=140,Height=34,Text="Iniciar viagem",Enabled=current==null};var finish=new Forms.Button{Left=166,Top=244,Width=140,Height=34,Text="Finalizar viagem",Enabled=current!=null};var cancel=new Forms.Button{Left=314,Top=244,Width=140,Height=34,Text="Cancelar viagem",Enabled=current!=null};
   start.Click+=(s,e)=>{try{if(!confirmed.Checked)throw new InvalidOperationException("Confirme o serviço de passageiros antes de iniciar.");if(_passengerLastRaw==null||(DateTime.UtcNow-_passengerLastAt).TotalSeconds>10)throw new InvalidOperationException("Aguardando telemetria atual.");store.Start(_passengerLastRaw,_accountUser,origin.Text,destination.Text);dialog.Close();}catch(Exception ex){Forms.MessageBox.Show(dialog,ex.Message,"Passageiros");}};
   finish.Click+=(s,e)=>{if(Forms.MessageBox.Show(dialog,"Confirma a conclusão do transporte? Pare o ônibus por pelo menos 10 segundos antes de finalizar.","Passageiros",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes)return;store.Command("finish");dialog.Close();};
   cancel.Click+=(s,e)=>{if(Forms.MessageBox.Show(dialog,"Cancelar este transporte sem recompensa?","Passageiros",Forms.MessageBoxButtons.YesNo)!=Forms.DialogResult.Yes)return;store.Command("cancel");dialog.Close();};
   dialog.Controls.Add(start);dialog.Controls.Add(finish);dialog.Controls.Add(cancel);dialog.ShowDialog(this);
  }
 }
 private static string PassengerMessage(string value){switch(value){case "brand_not_owned":return "Você precisa da chave da marca deste veículo na garagem.";case "brand_not_selected":return "Selecione na garagem a marca do veículo em uso.";case "passenger_finish_requires_stop":return "Pare o ônibus por pelo menos 10 segundos para finalizar.";case "passenger_distance_below_minimum":return "O trajeto ainda não atingiu a distância mínima da Central.";case "passenger_impossible_movement":case "passenger_odometer_reversed":return "A Central detectou uma inconsistência no trajeto. A viagem permanece em análise.";case "another_trip_active":return "Há outra viagem em andamento. Conclua ou cancele antes de iniciar passageiros.";default:return value.StartsWith("passenger_")?"A viagem está pendente de validação na Central.":value;}}
}
