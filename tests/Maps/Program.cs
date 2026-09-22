using KiwiDX;
using System.Reflection;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

class Program {
 [STAThread] static void Main() {
 ApplicationConfiguration.Initialize();
 using var host=new Form();
 host.Shown+=async(_,_)=>{
 try {
 using var map=new ReceiverMapForm();
 object Field(string n)=>typeof(ReceiverMapForm).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(map)!;
 var view=(WebView2)Field("webView");
 view.CoreWebView2InitializationCompleted+=(_,e)=>{
 if(!e.IsSuccess)return;
 view.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.Document);
 view.CoreWebView2.WebResourceRequested+=(_,args)=>{
 string html="<html><head></head><body><nav class='navbar'>Header</nav><div class='content-container container'><div class='map-container'><div class='gm-style-iw'><div class='infobox'><a id='rx' href='https://receiver.example:8073/'><h5>Receiver</h5></a></div></div></div></div><footer class='footer'>Footer</footer></body></html>";
 args.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html)),200,"OK","Content-Type: text/html");
 };
 };
 map.Show(host);
 for(int i=0;i<100 && view.CoreWebView2 is null;i++)await Task.Delay(100);
 var button=(Button)Field("switchMap");
 await Task.Delay(700);button.PerformClick();await Task.Delay(700);
 if(button.Text!="KiwiSDR map")throw new Exception("Wrong switch label");
 var hidden=await view.CoreWebView2!.ExecuteScriptAsync("getComputedStyle(document.querySelector('nav')).display==='none' && getComputedStyle(document.querySelector('footer')).display==='none'");
 if(hidden!="true")throw new Exception("Receiverbook chrome still visible");
 button.PerformClick();await Task.Delay(700);
 if(button.Text!="Receiverbook map")throw new Exception("Cannot return to KiwiSDR");
 button.PerformClick();await Task.Delay(700);
 string? selected=null;map.ServerSelected+=(_,url)=>selected=url;
 await view.CoreWebView2!.ExecuteScriptAsync("document.querySelector('#rx h5').click()");await Task.Delay(300);
 if(selected!="https://receiver.example:8073/")throw new Exception("Receiver selection failed");
 Console.WriteLine("PASS: map switching, hidden navbar/footer and receiver marker selection.");
 using var airspy=new ReceiverMapForm();
 var av=(WebView2)typeof(ReceiverMapForm).GetField("webView",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(airspy)!;
 airspy.Show(host);for(int i=0;i<100&&av.CoreWebView2 is null;i++)await Task.Delay(100);
 await Task.Delay(700);
 av.CoreWebView2!.NavigationStarting+=(_,e)=>Console.WriteLine("Airspy navigation: " + e.Uri[..Math.Min(e.Uri.Length,100)] + " cancel=" + e.Cancel);
 typeof(ReceiverMapForm).GetField("airspy",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(airspy,true);
 var assembly=typeof(ReceiverMapForm).Assembly;
 var service=assembly.GetType("KiwiDX.AirspyDirectoryService")!;
 var fixture = """{"servers":[{"streamingHost":"receiver.example","streamingPort":5555,"ownerName":"Airspy test receiver","online":true,"registered":true,"antennaLocation":{"lat":45,"long":-70}},{"streamingHost":"unreachable.example","streamingPort":5555,"ownerName":"Unreachable test receiver","online":true,"registered":false,"antennaLocation":{"lat":40,"long":-75}}]}""";
 var data=service.GetMethod("Parse",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{fixture})!;
 var html=(string)assembly.GetType("KiwiDX.AirspyMapPage")!.GetMethod("Create")!.Invoke(null,new[]{data})!;
 typeof(ReceiverMapForm).GetMethod("ShowAirspyEntries",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(airspy,new[]{data});
 for(int i=0;i<100;i++){await Task.Delay(100);if(await av.ExecuteScriptAsync("!!document.querySelector('#entries button')")=="true")break;}
 if(await av.ExecuteScriptAsync("document.querySelectorAll('#entries button').length")=="0")throw new Exception("Airspy receivers missing: " + await av.ExecuteScriptAsync("JSON.stringify({url:location.href,body:document.body.innerText,html:document.documentElement.outerHTML.slice(-1000)})"));
 if(await av.ExecuteScriptAsync("markers.get('sdr://receiver.example:5555').options.color==='#18b578' && markers.get('sdr://unreachable.example:5555').options.color==='#ef5350'")!="true")throw new Exception("Airspy readiness colors are wrong");
 await av.ExecuteScriptAsync("document.getElementById('readyOnly').click()");
 if(await av.ExecuteScriptAsync("document.querySelectorAll('#entries>button').length===1 && !map.hasLayer(markers.get('sdr://unreachable.example:5555'))")!="true")throw new Exception("Available-only filter failed");
 await av.ExecuteScriptAsync("document.getElementById('readyOnly').click()");
 if(await av.ExecuteScriptAsync("document.querySelectorAll('#entries>button').length===2 && map.hasLayer(markers.get('sdr://unreachable.example:5555'))")!="true")throw new Exception("Cannot restore unreachable receivers");
 Console.WriteLine("PASS: ready is green, unreachable is red; filter applies to both markers and list.");
 string? airspyUrl=null;airspy.ServerSelected+=(_,u)=>airspyUrl=u;
 await av.ExecuteScriptAsync("document.querySelector('#entries button').click()");
 if(await av.ExecuteScriptAsync("document.querySelector('.detail').textContent.includes('sdr://receiver.example:5555')")!="true")throw new Exception("SpyServer metadata missing");
 using(var screenshot=File.Create("dist/airspy-map.png"))await av.CoreWebView2!.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
 await av.ExecuteScriptAsync("document.querySelector('.detail button').click()");await Task.Delay(200);
 if(airspyUrl!="sdr://receiver.example:5555")throw new Exception("Airspy selection failed");
 Console.WriteLine("PASS: third map renders structured Airspy metadata and selects an sdr URL.");

 }catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}finally{host.Close();}
 };
 Application.Run(host);
 }
}
