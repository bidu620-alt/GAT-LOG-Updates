param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$file=Get-ChildItem -LiteralPath $Root -Filter 'VoiceClean.cs' -Recurse | Select-Object -First 1
if(-not $file){throw 'VoiceClean.cs nao encontrado.'}
$t=Get-Content -LiteralPath $file.FullName -Raw
if($t -notlike '*using System.Speech.Synthesis;*'){$t=$t.Replace('using System.Runtime.InteropServices;','using System.Runtime.InteropServices;'+[Environment]::NewLine+'using System.Speech.Synthesis;')}
if($t -notlike '*private SpeechSynthesizer _voiceCleanSpeech;*'){$t=$t.Replace('    private bool _voiceCleanConnectedLogged;','    private bool _voiceCleanConnectedLogged;'+[Environment]::NewLine+'    private SpeechSynthesizer _voiceCleanSpeech;')}
$t=$t.Replace('            JObject tele = JObject.Parse(raw);'+[Environment]::NewLine+'            VoiceCleanObserveRoad(tele, DateTime.UtcNow);','            JObject tele = JObject.Parse(raw);'+[Environment]::NewLine+'            TelemetryEngine.Normalize(tele);'+[Environment]::NewLine+'            VoiceCleanObserveRoad(tele, DateTime.UtcNow);')
$t=$t.Replace('double realLimit = VoiceCleanNumber(tele, "navigation.speedLimit", "Navigation.SpeedLimit");','double realLimit = VoiceCleanNumber(tele, "speed_limit_kmh", "navigation.speedLimit", "Navigation.SpeedLimit");'+[Environment]::NewLine+'        bool fallbackLimit = VoiceCleanBoolean(tele, "speed_limit_is_fallback");')
$t=$t.Replace('int eventLimit = VoiceCleanEventLimit(realLimit);','int eventLimit = VoiceCleanEventLimit(realLimit);')
$t=$t.Replace('            string eventName = "limite_" + eventLimit.ToString("D3", CultureInfo.InvariantCulture);'+[Environment]::NewLine+'            if (VoiceCleanPlayEvent(eventName, true))','            string eventName = "limite_" + eventLimit.ToString("D3", CultureInfo.InvariantCulture);'+[Environment]::NewLine+'            if (VoiceCleanPlayEvent(eventName, true))')
$helpers=@'
    private static bool VoiceCleanBoolean(JObject root, params string[] paths)
    {
        if (root == null) return false;
        foreach (string path in paths ?? new string[0])
        {
            try
            {
                JToken token = root.SelectToken(path, false);
                if (token == null || token.Type == JTokenType.Null) continue;
                if (token.Type == JTokenType.Boolean) return token.Value<bool>();
                if (bool.TryParse(token.ToString(), out bool result)) return result;
            }
            catch { }
        }
        return false;
    }

    private bool VoiceCleanSpeakFallbackLimit()
    {
        try
        {
            if (_voiceCleanMuted || _voiceCleanVolume <= 0) return false;
            VoiceCleanStop();
            if (_voiceCleanSpeech == null)
            {
                _voiceCleanSpeech = new SpeechSynthesizer();
                _voiceCleanSpeech.SetOutputToDefaultAudioDevice();
            }
            _voiceCleanSpeech.Volume = Math.Max(0, Math.Min(100, _voiceCleanVolume));
            _voiceCleanSpeech.Rate = 0;
            _voiceCleanSpeech.SpeakAsync("Atenção. Limite padrão da via: sessenta quilômetros por hora. Reduza a velocidade.");
            return true;
        }
        catch (Exception ex)
        {
            ClientStore.Log("VOZ limite padrão 60 indisponível: " + ex.Message);
            return false;
        }
    }

'@
if($t -notlike '*private bool VoiceCleanSpeakFallbackLimit()*'){
  $anchor='    private static string VoiceCleanEventFile(string eventName)'
  if(-not $t.Contains($anchor)){throw 'Ponto de insercao da voz nao reconhecido.'}
  $t=$t.Replace($anchor,$helpers+$anchor)
}
$t=$t.Replace('        try'+[Environment]::NewLine+'        {'+[Environment]::NewLine+'            string alias = _voiceCleanAlias;','        try'+[Environment]::NewLine+'        {'+[Environment]::NewLine+'            _voiceCleanSpeech?.SpeakAsyncCancelAll();'+[Environment]::NewLine+'            string alias = _voiceCleanAlias;')
$t=$t.Replace('            VoiceCleanStop();'+[Environment]::NewLine+'        };','            VoiceCleanStop();'+[Environment]::NewLine+'            try { _voiceCleanSpeech?.Dispose(); _voiceCleanSpeech = null; } catch { }'+[Environment]::NewLine+'        };')
foreach($m in @('TelemetryEngine.Normalize(tele);','speed_limit_is_fallback','VoiceCleanSpeakFallbackLimit','sessenta quilômetros por hora','_voiceCleanSpeech?.SpeakAsyncCancelAll()')){if($t -notlike "*$m*"){throw "Alerta de 60 incompleto: $m"}}
Set-Content -LiteralPath $file.FullName -Value $t -Encoding UTF8
Write-Host 'Alerta de voz: anuncia explicitamente limite padrão de 56 km/h; limite real mantém áudio existente.'
