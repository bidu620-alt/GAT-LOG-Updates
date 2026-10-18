using System;

namespace GatTelemetry;

// Display-only state: never used to validate deliveries or award points/Gold.
internal sealed class DashboardDistanceState
{
    private string _tripId;
    private double _startOdometerKm;
    public double DistanceKm { get; private set; }

    public void Observe(DashboardOdometerTrip trip, double odometerKm, bool connected)
    {
        if (trip == null || string.IsNullOrWhiteSpace(trip.TripId) || !connected) return;
        if (!string.Equals(_tripId, trip.TripId, StringComparison.Ordinal))
        {
            _tripId = trip.TripId;
            _startOdometerKm = Positive(trip.StartOdometerKm) ? trip.StartOdometerKm : 0;
            DistanceKm = 0;
        }
        if (trip.VehicleChanged || !Positive(odometerKm)) return;
        if (!Positive(_startOdometerKm)) _startOdometerKm = odometerKm;
        double driven = odometerKm - _startOdometerKm;
        if (driven >= 0 && !double.IsInfinity(driven)) DistanceKm = Math.Max(DistanceKm, driven);
    }

    private static bool Positive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
}

internal sealed class DashboardOdometerTrip
{
    public string TripId { get; }
    public double StartOdometerKm { get; }
    public bool VehicleChanged { get; }
    public DashboardOdometerTrip(string tripId, double startOdometerKm, bool vehicleChanged)
    {
        TripId = tripId;
        StartOdometerKm = startOdometerKm;
        VehicleChanged = vehicleChanged;
    }
}
