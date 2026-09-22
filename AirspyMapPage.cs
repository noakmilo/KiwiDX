using System.Text.Json;
namespace KiwiDX;

internal static class AirspyMapPage
{
    private static string Asset(string name) { using var stream = typeof(AirspyMapPage).Assembly.GetManifestResourceStream("KiwiDX.design.maps." + name)!; using var reader = new StreamReader(stream); return reader.ReadToEnd(); }
    public static string Create(SpyServerDirectoryEntry[] entries) => "<!doctype html><html><head><meta charset=\"utf-8\"><style>" + Asset("leaflet.css") + "</style><script>" + Asset("leaflet.js") + "</script>" + """
        <style>html,body{height:100%;margin:0;background:#171e25;color:#ddd;font:14px Segoe UI}#map{height:100%;margin-left:290px}#list{position:absolute;width:270px;top:0;bottom:0;padding:10px;overflow-y:auto;overflow-x:hidden}.detail{white-space:pre-line;overflow-wrap:anywhere}button,input{box-sizing:border-box}button,input{background:#28343e;color:#eee;border:1px solid #456;padding:8px;margin:3px 0;width:100%;text-align:left}button:enabled{cursor:pointer}button:disabled{opacity:.45}.leaflet-popup-content{white-space:pre-line}a{color:#39c98a}</style></head><body>
        <aside id="list"><h3>Airspy SpyServer</h3><div style="font-size:12px"><span style="color:#18b578">Green: Ready</span><br><span style="color:#ef5350">Red: Offline / unreachable</span><br><span style="color:#efad37">Orange: Busy</span><br><span style="color:#9aa6b2">Gray: Unverified</span></div><label><input id="readyOnly" type="checkbox" style="width:auto"> Only available receivers</label><input id="search" placeholder="Search receivers"><div id="entries"></div><small>Live data: airspy.com/directory</small></aside><div id="map"></div>
        <script>
        const servers =
        """ + JsonSerializer.Serialize(entries) + """
        ;const world =
        """ + Asset("world.geojson") + """
        ;let map;const markers=new Map();
        function initMap(){if(!window.L)return;map=L.map('map',{minZoom:1,maxZoom:10}).setView([25,0],2);L.geoJSON(world,{style:{color:'#526574',weight:1,fillColor:'#293e4b',fillOpacity:1},attribution:'Natural Earth (public domain)'}).addTo(map);document.getElementById('map').style.background='#132631';for(const s of servers){if(s.Latitude!==null&&s.Longitude!==null)markers.set(s.Url,L.circleMarker([s.Latitude,s.Longitude],{radius:5,color:s.StatusColor,fillColor:s.StatusColor,fillOpacity:.8}).addTo(map).bindPopup(details(s)))}}
        function connect(s){window.chrome.webview.postMessage(s.Url)}
        function details(s){const d=document.createElement('div');d.textContent=[s.Name,s.Device,s.Description,s.MaximumFrequency>s.MinimumFrequency?'Coverage: '+(s.MinimumFrequency/1e6)+' - '+(s.MaximumFrequency/1e6)+' MHz':'',s.Bandwidth>0?'Bandwidth: '+(s.Bandwidth/1e3)+' kHz':'',s.Antenna?'Antenna: '+s.Antenna:'',s.ServerVersion?'SpyServer '+s.ServerVersion:'',s.Url,s.StatusLabel+(s.MaxClients>0?' ('+s.Clients+'/'+s.MaxClients+')':'')].filter(Boolean).join('\n');const b=document.createElement('button');b.textContent='Connect';b.disabled=!s.Available;b.onclick=()=>connect(s);d.append(b);return d}
        
        function list(){for(const s of servers){const marker=markers.get(s.Url);if(marker){if(!document.getElementById('readyOnly').checked||s.Available)marker.addTo(map);else marker.remove()}}const root=document.getElementById('entries');root.replaceChildren();const q=document.getElementById('search').value.toLowerCase();for(const s of servers){if(document.getElementById('readyOnly').checked&&!s.Available)continue;if(!(s.Name+' '+s.Device+' '+s.Description+' '+s.Url).toLowerCase().includes(q))continue;const b=document.createElement('button');b.textContent='\u25cf '+(s.Name||s.Url)+' - '+s.StatusLabel;b.style.borderLeft='4px solid '+s.StatusColor;b.onclick=()=>{root.querySelectorAll('.detail').forEach(x=>x.remove());const d=details(s);d.className='detail';b.after(d);if(map&&s.Latitude!==null&&s.Longitude!==null)map.setView([s.Latitude,s.Longitude],5)};root.append(b)}}document.getElementById('search').oninput=list;document.getElementById('readyOnly').onchange=list;list();
        initMap();
        </script></body></html>
        """;
}
