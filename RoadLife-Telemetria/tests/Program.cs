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
