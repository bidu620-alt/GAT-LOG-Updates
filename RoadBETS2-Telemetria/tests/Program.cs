using System;
using GatTelemetry;

int checks = 0;
void Check(double actual, double expected) { checks++; if (actual != expected) throw new Exception($"Expected {expected}, got {actual}"); }
var state = new RoadLimitState();
double Get(double raw, double t, bool connected = true) => state.Resolve(connected, raw, t, out _);
Check(Get(double.NaN, 0), 60);
Check(Get(90, 1), 90);
Check(Get(0, 2), 90);
Check(Get(0, 61.999), 90);
Check(Get(0, 62), 60);
Check(Get(80, 63), 80);
Check(Get(0, 64), 60); // a short valid burst cannot renew the grace budget
Check(Get(70, 65), 70);
Check(Get(70, 125), 70); // 60 seconds of stable valid limits re-arms it
Check(Get(0, 126), 70);
Check(Get(0, 186), 60);
Check(Get(50, 187), 50); // a lower valid limit applies immediately
Check(Get(0, 188, false), 0);
Check(Get(0, 189), 60);
Check(Get(double.PositiveInfinity, 190), 60);
Check(Get(-10, 191), 60);
state = new RoadLimitState();
Check(Get(90, 0), 90);
Check(Get(0, 1), 90);
Check(Get(90, 31), 90);
Check(Get(0, 32), 90);
Check(Get(0, 62), 60); // missing durations accumulate across brief valid bursts
Console.WriteLine($"{checks} checks passed.");

var distance = new DashboardDistanceState();
var trip = new DashboardOdometerTrip("trip-one", 50000, false);
distance.Observe(trip, 50000, true); Check(distance.DistanceKm, 0);
distance.Observe(trip, 50150.5, true); Check(distance.DistanceKm, 150.5);
// GPS and planned distance are deliberately not inputs: rerouting cannot reset the counter.
distance.Observe(trip, 51200, true); Check(distance.DistanceKm, 1200);
distance.Observe(trip, 51400, true); Check(distance.DistanceKm, 1400);
distance.Observe(trip, 0, false); Check(distance.DistanceKm, 1400);
distance.Observe(trip, double.NaN, true); Check(distance.DistanceKm, 1400);
distance.Observe(trip, double.PositiveInfinity, true); Check(distance.DistanceKm, 1400);
distance.Observe(trip, 49990, true); Check(distance.DistanceKm, 1400);
distance.Observe(trip, 51399.9, true); Check(distance.DistanceKm, 1400);
distance.Observe(new DashboardOdometerTrip("trip-one", 50000, true), 900000, true); Check(distance.DistanceKm, 1400);
// Delivery updates the last sample; after the trip clears, idle driving does not add kilometers.
distance.Observe(trip, 51410, true); Check(distance.DistanceKm, 1410);
distance.Observe(null, 51420, true); Check(distance.DistanceKm, 1410);
distance.Observe(new DashboardOdometerTrip("trip-two", 51420, false), 51420, true); Check(distance.DistanceKm, 0);
distance.Observe(new DashboardOdometerTrip("trip-two", 51420, false), 51445, true); Check(distance.DistanceKm, 25);
// Restored journal supplies the original baseline after restarting the app.
distance = new DashboardDistanceState();
distance.Observe(new DashboardOdometerTrip("restored-trip", 20000, false), 20600, true); Check(distance.DistanceKm, 600);
distance = new DashboardDistanceState();
distance.Observe(new DashboardOdometerTrip("missing-first-odometer", -1, false), 0, true); Check(distance.DistanceKm, 0);
distance.Observe(new DashboardOdometerTrip("missing-first-odometer", -1, false), 7000, true); Check(distance.DistanceKm, 0);
distance.Observe(new DashboardOdometerTrip("missing-first-odometer", 7000, false), 7030, true); Check(distance.DistanceKm, 30);
Console.WriteLine($"{checks} total checks passed, including odometer display continuity and new-trip reset.");

foreach (var old in new[]{"https://api.gatlogets2.com.br","http://api.gatlogets2.com.br","https://API.GATLOGETS2.COM.BR"}) { if(CentralEndpointMigration.Resolve(old)!=CentralEndpointMigration.OfficialApi)throw new Exception("Saved official endpoint not migrated"); }
foreach (var other in new[]{"https://api.bidulog.com.br","https://driver.example.com","http://127.0.0.1:5056","https://api.gatlogets2.com.br.example.com"}) { if(CentralEndpointMigration.Resolve(other)!=other)throw new Exception("Unrelated endpoint changed"); }
Console.WriteLine("7 endpoint migration cases passed; saved credentials keep matching and custom servers stay unchanged.");

var vehicleCases=new[]{new[]{"Scania","R","scania","","scania"},new[]{"Mercedes-Benz","Actros","mercedes.actros","","mercedes"},new[]{"Volvo","FH6","volvo","","volvo"},new[]{"Marcopolo","G8 1800 DD","marcopolo.g8","","onibus"},new[]{"Scania","Marcopolo G7","scania.k360","","onibus"},new[]{"Mercedes-Benz","Paradiso 1200","mb.o500","","onibus"},new[]{"Busscar","Vissta Buss","busscar","","onibus"},new[]{"Volvo","B12R","volvo.b12r","","onibus"},new[]{"Scania","K 360","scania","","onibus"},new[]{"Custom","Modelo desconhecido","x","coach","onibus"}};
foreach(var v in vehicleCases)if(VehicleEconomyKey.Resolve(v[0],v[1],v[2],v[3])!=v[4])throw new Exception("Vehicle classification: "+v[0]+" "+v[1]);
Console.WriteLine("10 vehicle classification cases passed: truck brands, Marcopolo G7/G8, bus chassis and explicit vehicle type.");
