using System;

namespace GatTelemetry;

internal static class CentralEndpointMigration
{
    public const string OfficialApi = "https://api.bidulog.com.br";

    // Map saved server/credential keys in memory; do not erase credentials or journal.
    public static string Resolve(string endpoint)
    {
        if (string.Equals(endpoint, "https://api.gatlogets2.com.br", StringComparison.OrdinalIgnoreCase)
            || string.Equals(endpoint, "http://api.gatlogets2.com.br", StringComparison.OrdinalIgnoreCase))
            return OfficialApi;
        return endpoint;
    }
}
