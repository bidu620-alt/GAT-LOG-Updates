package com.gatlog.dash;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.tts.TextToSpeech;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends Activity {
    private static final String CENTRAL_LOGIN = "https://api.gatlogets2.com.br/api/account/login";
    private final ExecutorService telemetryIo = Executors.newSingleThreadExecutor();
    private final ExecutorService requestIo = Executors.newCachedThreadPool();
    private final Handler ui = new Handler(Looper.getMainLooper());
    private WebView web;
    private TextToSpeech tts;
    private SharedPreferences prefs;
    private volatile String telemetryHost = "";
    private volatile boolean voiceEnabled = true;
    private volatile int tolerance = 3;
    private volatile boolean running = true;

    @Override public void onCreate(Bundle b) {
        super.onCreate(b);
        prefs = getSharedPreferences("gat_dash", Context.MODE_PRIVATE);
        telemetryHost = sanitizeHost(prefs.getString("host", ""));
        voiceEnabled = prefs.getBoolean("voice", true);
        tolerance = prefs.getInt("tolerance", 3);
        tts = new TextToSpeech(this, status -> { if(status == TextToSpeech.SUCCESS) tts.setLanguage(new Locale("pt","BR")); });

        web = new WebView(this);
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true); s.setDomStorageEnabled(true); s.setAllowFileAccess(true); s.setAllowContentAccess(true);
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW); s.setMediaPlaybackRequiresUserGesture(false); s.setJavaScriptCanOpenWindowsAutomatically(true);
        CookieManager.getInstance().setAcceptCookie(true);
        CookieManager.getInstance().setAcceptThirdPartyCookies(web, true);
        web.setWebViewClient(new WebViewClient() {
            @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                try {
                    Uri uri = request.getUrl();
                    if(uri != null && "gatdash.local".equalsIgnoreCase(uri.getHost())) {
                        String path = uri.getPath();
                        if(path == null || path.equals("/") || path.isEmpty()) path = "/index.html";
                        path = path.startsWith("/") ? path.substring(1) : path;
                        String mime = path.endsWith(".css") ? "text/css" : path.endsWith(".js") ? "application/javascript" : "text/html";
                        return new WebResourceResponse(mime, "UTF-8", getAssets().open(path));
                    }
                } catch(Exception ignored) { }
                return super.shouldInterceptRequest(view, request);
            }
        });
        web.addJavascriptInterface(new Bridge(), "GatAndroid");
        setContentView(web);
        web.loadUrl("https://gatdash.local/index.html");
        startTelemetryLoop();
    }

    @Override protected void onDestroy() {
        running = false;
        if(tts != null) { tts.stop(); tts.shutdown(); }
        telemetryIo.shutdownNow(); requestIo.shutdownNow();
        if(web != null) web.destroy();
        super.onDestroy();
    }

    private void startTelemetryLoop() {
        telemetryIo.submit(() -> {
            while(running) {
                try {
                    if(!telemetryHost.isEmpty()) {
                        String host = telemetryHost;
                        String url = "http://" + host + (host.contains(":") ? "" : ":31377") + "/api/ets2/telemetry";
                        String json = httpGet(url, 2500);
                        js("window.gatDashPushTelemetry(" + JSONObject.quote(json) + ")");
                    } else js("window.gatDashTelemetryError('configure o IP do PC')");
                } catch(Exception e) { js("window.gatDashTelemetryError(" + JSONObject.quote(shortError(e.getMessage())) + ")"); }
                try { Thread.sleep(400); } catch(InterruptedException e) { return; }
            }
        });
    }

    private final class Bridge {
        @JavascriptInterface public void postMessage(String raw) {
            try {
                JSONObject m = new JSONObject(raw); String type = m.optString("type", "");
                if("ready".equals(type)) js("window.gatDashNativeReady('android'," + settingsJson().toString() + ")");
                else if("login".equals(type)) login(m.optString("user",""), m.optString("password",""));
                else if("setSettings".equals(type)) {
                    telemetryHost = sanitizeHost(m.optString("host", "")); voiceEnabled = m.optBoolean("voice", true); tolerance = Math.max(0, Math.min(30, m.optInt("tolerance", 3)));
                    prefs.edit().putString("host", telemetryHost).putBoolean("voice", voiceEnabled).putInt("tolerance", tolerance).apply();
                    js("window.gatDashSettings(" + settingsJson().toString() + ")");
                } else if("speak".equals(type) && voiceEnabled) {
                    String text = m.optString("text", "").trim(); if(!text.isEmpty() && text.length() < 240) ui.post(() -> { if(tts != null) tts.speak(text, TextToSpeech.QUEUE_FLUSH, null, "gat-speed"); });
                } else if("openUrl".equals(type)) {
                    String value=m.optString("url","").trim();
                    if(value.startsWith("https://") || value.startsWith("http://")) ui.post(() -> { try { startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(value))); } catch(Exception ignored) {} });
                } else if("mediaRefresh".equals(type)) {
                    requestIo.submit(() -> pollMedia());
                }
            } catch(Exception ignored) { }
        }
    }

    private void pollMedia() {
        try {
            if(telemetryHost.isEmpty()) { js("window.gatDashMediaError('configure o IP do PC')"); return; }
            String host = telemetryHost;
            String url = "http://" + host + (host.contains(":") ? "" : ":31378") + "/api/gat/media?t=" + System.currentTimeMillis();
            String json = httpGet(url, 2500);
            js("window.gatDashPushMedia(" + JSONObject.quote(json) + ")");
        } catch(Exception e) {
            js("window.gatDashMediaError('abra o GAT Telemetria no PC')");
        }
    }

    private void login(String user, String password) {
        requestIo.submit(() -> {
            try {
                JSONObject body = new JSONObject(); body.put("user", user.trim().toLowerCase(Locale.ROOT)); body.put("password", password);
                String result = httpPostJson(CENTRAL_LOGIN, body.toString(), 6000);
                js("window.gatDashLoginResult(" + JSONObject.quote(result) + ")");
            } catch(Exception e) { js("window.gatDashLoginTransportError(" + JSONObject.quote(shortError(e.getMessage())) + ")"); }
        });
    }

    private JSONObject settingsJson() {
        JSONObject o = new JSONObject(); try { o.put("host", telemetryHost); o.put("voice", voiceEnabled); o.put("tolerance", tolerance); } catch(Exception ignored) { } return o;
    }
    private void js(String script) { ui.post(() -> { if(web != null) web.evaluateJavascript(script, null); }); }
    private static String sanitizeHost(String v) { String h=v==null?"":v.trim().replace("http://","").replace("https://",""); while(h.endsWith("/"))h=h.substring(0,h.length()-1); int slash=h.indexOf('/');if(slash>=0)h=h.substring(0,slash); return h.length()<=120?h:""; }
    private static String httpGet(String url,int timeout)throws Exception{HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();c.setConnectTimeout(timeout);c.setReadTimeout(timeout);c.setRequestMethod("GET");try{return read(c.getResponseCode()>=400?c.getErrorStream():c.getInputStream());}finally{c.disconnect();}}
    private static String httpPostJson(String url,String body,int timeout)throws Exception{HttpURLConnection c=(HttpURLConnection)new URL(url).openConnection();c.setConnectTimeout(timeout);c.setReadTimeout(timeout);c.setRequestMethod("POST");c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json; charset=utf-8");byte[] data=body.getBytes(StandardCharsets.UTF_8);c.setFixedLengthStreamingMode(data.length);try(OutputStream os=c.getOutputStream()){os.write(data);}String r=read(c.getResponseCode()>=400?c.getErrorStream():c.getInputStream());c.disconnect();return r;}
    private static String read(InputStream in)throws Exception{if(in==null)return"";StringBuilder b=new StringBuilder();try(BufferedReader r=new BufferedReader(new InputStreamReader(in,StandardCharsets.UTF_8))){String line;while((line=r.readLine())!=null)b.append(line);}return b.toString();}
    private static String shortError(String m){if(m==null||m.trim().isEmpty())return"sem conexão";return m.length()>90?m.substring(0,90)+"…":m;}
}
