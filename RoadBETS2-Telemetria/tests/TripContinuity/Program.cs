using System;
using System.IO;
using GatTelemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var testDir = Path.Combine(Path.GetTempPath(), "bidutruck-trip-continuity-" + Guid.NewGuid().ToString("N"));
ClientStore.DataDir = testDir;
Directory.CreateDirectory(testDir);
try {
    int checks=0;
    void Require(bool condition, string why) { checks++; if(!condition) throw new Exception(why); }
    // Production-like checkpoint: first 30 km were already observed and saved before restart.
    string tripId=Guid.NewGuid().ToString("N");
    var trip=new TripReceipt {
       TripId=tripId, ProfileId="profile_ets2", CargoId="salt_spices",
       Cargo="Sal e temperos", Source="개성", Destination="Port Sudan",
       TruckId="test-truck-001", TruckIdentity="id:test-truck-001",
       StartOdometerKm=50000, EndOdometerKm=50030, DrivenDistanceKm=30,
       StartedObservedAt=DateTime.UtcNow.AddMinutes(-40).ToString("o"),
       LastOdometerObservedAt=DateTime.UtcNow.ToString("o"), OdometerVerified=true,
    };
    var state=new TripJournalState { ActiveTrip=trip, EventsInitialized=true };
    File.WriteAllText(Path.Combine(testDir,"trip_journal.json"),JsonConvert.SerializeObject(state));
    JObject active = JObject.Parse(@"{'on_job':true,'cargo_name':'Sal e temperos','source_city':'개성','destination_city':'Port Sudan','cargo_id':'salt_spices','gat_profile_id':'profile_ets2','truck':{'id':'test-truck-001'},'gameplay':{'onJob':true}}");
    var journal1 = new TripJournal();
    Require(journal1.ReadActiveTripIdForCargo(active) == tripId,"first session must reference saved ID");
    // Disposing the old process and reinitializing the TripJournal simulates app restart.
    var journal2 = new TripJournal();
    Require(journal2.ReadActiveTripIdForCargo(active) == tripId,"restart must keep original ID");
    Require(journal2.ReadActiveTripIdForCargo(JObject.Parse(@"{'on_job':true,'cargo_name':'Sal e temperos','source_city':'개성','destination_city':'Port Sudan','cargo_id':'salt_spices','gat_profile_id':'other-profile','truck':{'id':'test-truck-001'}}")) == "","must not match a different profile");
    Require(journal2.ReadActiveTripIdForCargo(JObject.Parse(@"{'on_job':true,'cargo_name':'Querosene','source_city':'개성','destination_city':'Port Sudan','cargo_id':'salt_spices','gat_profile_id':'profile_ets2','truck':{'id':'test-truck-001'}}")) == "","must not match a different cargo");
    Require(journal2.ReadActiveTripIdForCargo(JObject.Parse(@"{'on_job':true,'cargo_name':'Sal e temperos','source_city':'개성','destination_city':'Port Sudan','cargo_id':'salt_spices','gat_profile_id':'profile_ets2','truck':{'id':'other-truck'}}")) == tripId,"stable profile controls identity on restored trip");
    Require(journal2.ReadActiveTripIdForCargo(JObject.Parse(@"{'on_job':false,'cargo_name':'Sal e temperos','source_city':'개성','destination_city':'Port Sudan','gat_profile_id':'profile_ets2','truck':{'id':'test-truck-001'}}")) == "","idle cannot claim an active trip");
    // Central and journal must share one ID; this regression test guards the source wiring too.
    var formSource=File.ReadAllText(Path.Combine("RoadBETS2-Telemetria","src","GatTelemetry","MainForm.cs"));
    Require(formSource.Contains("string journalTripId = _tripJournal.ReadActiveTripIdForCargo(tele);"),"Central flow must consult journal");
    Require(formSource.Contains("string.IsNullOrWhiteSpace(journalTripId) ? Guid.NewGuid().ToString"),"fallback IDs only when no matching journal trip");
    Console.WriteLine($"PASS: {checks} restarted-trip identity assertions");
} finally { try { Directory.Delete(testDir,true); } catch {} }
