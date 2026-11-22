using System;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace GatTelemetry;
internal sealed partial class MainForm
{
    private JObject _neonOpeningFrame;
    private long _neonOpeningFrameAt;
    private bool _neonInteractiveSizing;
    // Account telemetry can seed opening, but never competes with the live dashboard feed.
    private void RememberNeonOpeningFrame(JObject telemetry)
    {
        if(telemetry==null)return;
        try {
            var payload=RoadLifeOverlayBuildPayload(telemetry);
            System.Threading.Volatile.Write(ref _neonOpeningFrame,payload);
            System.Threading.Interlocked.Exchange(ref _neonOpeningFrameAt,System.Diagnostics.Stopwatch.GetTimestamp());
        }catch(Exception ex){ClientStore.Log("Dashboard opening frame: "+ex.GetType().Name);}
    }
    private void PrepareNeonOpeningFrame()
    {
        long age=System.Diagnostics.Stopwatch.GetTimestamp()-System.Threading.Interlocked.Read(ref _neonOpeningFrameAt);
        var frame=System.Threading.Volatile.Read(ref _neonOpeningFrame);
        if(frame!=null&&age>=0&&age<3*System.Diagnostics.Stopwatch.Frequency)
            ApplyRoadLifePayload(frame);
        SetRoadLifeText("DateText",DateTime.Now.ToString("dd/MM/yy"));
        SetRoadLifeText("TimeText",DateTime.Now.ToString("HH:mm:ss"));
    }
    private bool ApplyNeonLiveFrame(Window expected,JObject telemetry)
    {
        if(expected==null||!ReferenceEquals(expected,_roadLifeOverlay)||_neonInteractiveSizing)return false;
        if(telemetry==null){ApplyRoadLifePayload(new JObject{["display"]=new JObject{["connected"]=false}});return true;}
        ApplyRoadLifePayload(RoadLifeOverlayBuildPayload(telemetry));return true;
    }
}
