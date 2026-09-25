using System;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;

internal sealed class ApiResponse
{
	public int StatusCode { get; set; }

	public JObject Json { get; set; }

	public string Text { get; set; }

	public Exception Error { get; set; }

	public bool Ok
	{
		get
		{
			if (StatusCode >= 200 && StatusCode < 300)
			{
				return Error == null;
			}
			return false;
		}
	}
}
