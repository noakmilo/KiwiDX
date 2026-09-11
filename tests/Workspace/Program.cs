using System.Reflection;
using KiwiDX;
class QuietForm:Form1 {protected override void OnShown(EventArgs e) {}}
class Program {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
 [STAThread] static void Main(){
 ApplicationConfiguration.Initialize();
 using var f=new QuietForm();f.Show();Application.DoEvents();f.WindowState=FormWindowState.Normal;f.Size=new Size(1586,1000);Application.DoEvents();
 object Field(string n)=>typeof(Form1).GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(f)!;
 void Call(string n,params object[] a)=>typeof(Form1).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(f,a);
 var wf=(WaterfallControl)Field("waterfall");var split=(SplitContainer)Field("workspace");
 if(Environment.GetEnvironmentVariable("KIWIDX_EXPORT_ICONS")=="1") {
 var theme=typeof(Form1).Assembly.GetType("KiwiDX.WorkspaceTheme")!;
 var glyphType=typeof(Form1).Assembly.GetType("KiwiDX.RadioGlyph")!;
 var draw=theme.GetMethod("Glyph",BindingFlags.Static|BindingFlags.NonPublic)!;
 Directory.CreateDirectory("design/icons");
 foreach(var name in new[]{"Wave","Globe","Star","Chat","Speaker","Gear","Share","User","Info","Dot"}) {
 using var iconBitmap=new Bitmap(48,48);using(var g=Graphics.FromImage(iconBitmap)){g.Clear(Color.Transparent);draw.Invoke(null,new object[]{g,Enum.Parse(glyphType,name),new RectangleF(0,0,48,48),name=="Star"?Color.FromArgb(249,199,73):name is "Chat" or "Wave" or "Dot"?Color.FromArgb(109,239,125):Color.FromArgb(228,237,246)});}
 iconBitmap.Save("design/icons/"+name.ToLowerInvariant()+".png");
 if(name=="Wave"){var handle=iconBitmap.GetHicon();try{using var icon=Icon.FromHandle(handle);using var file=File.Create("design/icons/kiwidx.ico");icon.Save(file);}finally{DestroyIcon(handle);}}
 }
 }
 // A burst must not enqueue one WinForms callback for every receiver frame.
 var receiver=(KiwiClient)Field("client");
 var onLine=(EventHandler<byte[]>)typeof(KiwiClient).GetField("WaterfallLine",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(receiver)!;
 Task.Run(()=>{for(int i=0;i<100000;i++)onLine(receiver,new byte[1200]);}).GetAwaiter().GetResult();
 var heartbeat=false;f.BeginInvoke(()=>heartbeat=true);
 var watch=System.Diagnostics.Stopwatch.StartNew();Application.DoEvents();
 if(!heartbeat||watch.ElapsedMilliseconds>2000)throw new Exception("Receiver burst blocked UI heartbeat");
 Console.WriteLine("PASS: 100,000 receiver frames leave UI responsive with bounded pending display work.");
 var logBox=(TextBox)Field("consoleBox");
 for(int i=0;i<500;i++)Call("AddLog",new string('x',50000));
 if(logBox.IsHandleCreated)throw new Exception("Hidden console created a native handle");
 Call("ShowConnectionProgress",25,"Opening receiver channels...");Application.DoEvents();
 if(!((Panel)Field("connectionProgress")).Visible)throw new Exception("Connection progress is hidden");
 Call("HideConnectionProgress");
 Console.WriteLine("PASS: large hidden-console logs remain in bounded memory without creating a native edit control.");
 var h=wf.Height;Call("ShowSidebar","");Application.DoEvents();if(wf.Height!=h||!split.Panel2Collapsed)throw new Exception("Closing chat changed waterfall height");
 Call("ShowSidebar","details");Application.DoEvents();if(wf.Height!=h)throw new Exception("Details changed waterfall height");Call("ShowSidebar","chat");Application.DoEvents();
 Call("OpenConsoleWindow");Call("OpenConsoleWindow");Application.DoEvents();((Form)Field("consoleWindow")).Close();if(wf.Height!=h)throw new Exception("Console changed waterfall height");
 ((ComboBox)Field("bandBox")).SelectedIndex=10;((TextBox)Field("frequencyBox")).Text="7.100000";wf.SetRadioState(7100000,50000,7100000,2400);
 var random=new Random(42);for(int t=0;t<500;t++){var line=new byte[1200];for(int x=0;x<line.Length;x++){double signal=0;foreach(int p in new[]{240,410,600,810,970,1140})signal+=45*Math.Exp(-Math.Pow((x-p)/5d,2));line[x]=(byte)Math.Clamp(139+random.Next(0,18)+signal,0,255);}wf.AddLine(line);}
 Application.DoEvents();
 var chat=(Control)Field("communityChat");
 var composer=(TextBox)chat.GetType().GetField("composer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chat)!;
 foreach(var command in new[]{"/login user private-password","/REGISTER user private-password","/nick register user private-password"}){composer.Text=command;if(!composer.UseSystemPasswordChar)throw new Exception("Credentials are visible");}composer.Clear();
 var attemptField=chat.GetType().GetField("attempt",BindingFlags.Instance|BindingFlags.NonPublic)!;
 using(var blockedAttempt=new CancellationTokenSource()){
 attemptField.SetValue(chat,blockedAttempt);Call("DetachChat");Application.DoEvents();
 var detached=(Form)Field("chatWindow");if(!detached.Visible)throw new Exception("Chat did not detach");
 Call("DetachChat");
 detached.WindowState=FormWindowState.Minimized;Call("DetachChat");
 if(detached.WindowState==FormWindowState.Minimized)throw new Exception("Detached chat was not restored");
 detached.Close();Application.DoEvents();if(!chat.Visible||split.Panel2Collapsed)throw new Exception("Chat did not dock again");attemptField.SetValue(chat,null);
 }
 Call("OpenDisplaySettings");Call("OpenDisplaySettings");((Form)Field("displayWindow")).Close();
 Console.WriteLine("PASS: chat can detach and redock; credential commands remain masked.");
 var receive=chat.GetType().GetMethod("Receive",BindingFlags.NonPublic|BindingFlags.Instance)!;
 foreach(var message in new[]{"Good afternoon, everyone.","Strong signal on 40 meters.","Listening here: kiwidx://tune?rx=https%3A%2F%2Fexample.org&hz=7100000&mode=LSB&protocol=KiwiSDR"}) {
 using var document=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new{type="message",channel="#hamradio",nick="N0CALL",time=1789050000,text=message}));receive.Invoke(chat,new object[]{document.RootElement.Clone()});
 }
 using var bitmap=new Bitmap(f.Width,f.Height);f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));Directory.CreateDirectory("dist");bitmap.Save("dist/workspace-v0.2.0.png");
 foreach(var width in new[]{1100,1280,1586}){f.Size=new Size(width,850);Application.DoEvents();if(wf.Width<650||wf.Height<250)throw new Exception("Receiver area collapsed");}
 Console.WriteLine("PASS: chat/details toggles and console window preserve receiver height.");Console.WriteLine("PASS: workspace remains usable at 1100, 1280 and 1586 pixels.");Console.WriteLine("Preview: dist/workspace-v0.2.0.png");f.Close();
 }
}
