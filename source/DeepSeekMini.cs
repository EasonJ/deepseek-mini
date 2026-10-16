using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Markup;
using System.Windows.Automation;
using System.Xml.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms = System.Windows.Forms;

namespace DeepSeekMini {
 public class Preferences {
  public double Width = 620, Height = 700;
  public int LayoutVersion = 0;
  public uint Modifiers = 3, Key = 68;
  public bool Pinned = false;
  public bool RememberPosition = true, HideOnDeactivate = false, EscapeHides = true, MemorySaver = false, Preload = false;
  public double Left = double.NaN, Top = double.NaN;
 }
 public static class Program {
  internal static readonly System.Diagnostics.Stopwatch Clock=System.Diagnostics.Stopwatch.StartNew();
  [STAThread] public static void Main(string[] args) {
   bool first;
   using(var mutex = new Mutex(true, @"Local\DeepSeekMini.Desktop.v1", out first)) {
    if(!first) { Native.PostMessage(new IntPtr(0xffff), Native.ShowMessage, IntPtr.Zero, IntPtr.Zero); return; }
    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    app.DispatcherUnhandledException += (s,e) => { MainWindow.Log(e.Exception.ToString()); MessageBox.Show("发生错误："+e.Exception.Message, "DeepSeek Mini"); e.Handled=true; };
    var win = new MainWindow(args.Contains("--background"));
    app.Run();
   }
  }
 }
 internal static class Native {
  [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
  [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string text);
  internal static readonly uint ShowMessage=RegisterWindowMessage("DeepSeekMini.Desktop.Show.v1");
  [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
 }
 public partial class MainWindow : Window {
  internal static readonly string Data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
  static readonly string Config = Path.Combine(Data,"settings.xml");
  WebView2 web;
  readonly TextBlock status = new TextBlock { Margin=new Thickness(14,8,14,8), FontSize=11, Foreground=new SolidColorBrush(Color.FromRgb(146,150,160)), TextTrimming=TextTrimming.CharacterEllipsis };
  readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
  System.Drawing.Icon whaleIcon;
  Preferences prefs;
  IntPtr handle;
  HwndSource source;
  bool quitting, initialized, starting, registered;
  int hotkeyId=1;
  Button pin;
  Window settings;
  public static void Log(string text) { try { Directory.CreateDirectory(Data); File.AppendAllText(Path.Combine(Data,"diagnostics.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine); } catch {} }
  static Preferences Load() { try { using(var r=File.OpenRead(Config)) return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(r); } catch {return new Preferences();} }
  public MainWindow(bool background) {
   Directory.CreateDirectory(Data); prefs=Load();Log("Starting; background="+background);
   if(prefs.LayoutVersion<2){prefs.Width=Math.Max(620,prefs.Width);prefs.Height=Math.Max(700,prefs.Height);prefs.LayoutVersion=2;}
   Title="DeepSeek Mini"; Width=Math.Max(380,Math.Min(1400,prefs.Width)); Height=Math.Max(440,Math.Min(1200,prefs.Height));
   MinWidth=380; MinHeight=440; ShowInTaskbar=true; Topmost=prefs.Pinned;ApplyTheme(this,true);
   var logo=new BitmapImage();
   using(var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("Whale.png")){logo.BeginInit();logo.CacheOption=BitmapCacheOption.OnLoad;logo.StreamSource=stream;logo.EndInit();logo.Freeze();}
   Icon=logo;
   using(var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("Whale.ico"))using(var icon=new System.Drawing.Icon(stream,32,32)){whaleIcon=(System.Drawing.Icon)icon.Clone();}
   var root=new DockPanel(); Content=new Border{BorderBrush=Brush("#35363B"),BorderThickness=new Thickness(1),Child=root};
   var header=new Grid{Height=52,Background=Brush("#202126")};
   header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
   var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(15,0,0,0),VerticalAlignment=VerticalAlignment.Center};header.Children.Add(brand);
   var brandIcon=new Image{Width=32,Height=32,Source=logo};RenderOptions.SetBitmapScalingMode(brandIcon,BitmapScalingMode.NearestNeighbor);brand.Children.Add(brandIcon);
   brand.Children.Add(new TextBlock{Text="DeepSeek",FontSize=13,FontWeight=FontWeights.SemiBold,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center});
   var toolbar=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,7,0)};Grid.SetColumn(toolbar,1);header.Children.Add(toolbar);
   pin=IconButton(toolbar,"\uE718",prefs.Pinned?"取消置顶":"置顶",()=>{Topmost=!Topmost;prefs.Pinned=Topmost;pin.Foreground=Topmost?Brush("#90ADFF"):Brush("#DADCE5");pin.ToolTip=Topmost?"取消置顶":"置顶";Save();});
   pin.Foreground=Topmost?Brush("#90ADFF"):Brush("#DADCE5");
   IconButton(toolbar,"\uE70F","快捷草稿",()=>OpenDraft(false));
   IconButton(toolbar,"\uE713","设置",OpenSettings);
   toolbar.Children.Add(new Border{Width=1,Height=16,Background=Brush("#3A3B40"),Margin=new Thickness(5,0,5,0)});
   IconButton(toolbar,"\uE921","隐藏到托盘",HidePanel);
   var maximize=IconButton(toolbar,"\uE922","最大化 / 还原",()=>{WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;});
   StateChanged+=(s,e)=>{maximize.Content=WindowState==WindowState.Maximized?"\uE923":"\uE922";sidebarRequested=WindowState==WindowState.Maximized;sidebarEpoch++;if(sidebarRequested)ExpandSidebar();};
   IconButton(toolbar,"\uE8BB","关闭并隐藏到托盘",()=>Close());
   var footer=new Border{Background=Brush("#202126"),BorderBrush=Brush("#2D2E33"),BorderThickness=new Thickness(0,1,0,0),Child=status};
   DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(webHost);
   webHost.Children.Add(new TextBlock{Text="正在准备 DeepSeek…",Foreground=Brush("#A1A5B2"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center});
   var menu=new Forms.ContextMenuStrip();
   menu.Items.Add("显示窗口",null,(s,e)=>ShowPanel());
   menu.Items.Add("剪贴板快捷问答 · Ctrl+Alt+Q",null,(s,e)=>OpenDraft(true));
   menu.Items.Add("刷新网页",null,(s,e)=>{ShowPanel();if(initialized)web.Reload();else Initialize();});
   menu.Items.Add("设置 / 修改快捷键",null,(s,e)=>OpenSettings());
   menu.Items.Add("退出",null,(s,e)=>Quit());
   tray.Icon=whaleIcon;tray.Text="DeepSeek Mini";tray.ContextMenuStrip=menu;tray.Visible=true;
   tray.DoubleClick+=(s,e)=>ShowPanel();
   handle=new WindowInteropHelper(this).EnsureHandle(); source=HwndSource.FromHwnd(handle);source.AddHook(Hook);
   ApplyCorners(handle);
   registered=Native.RegisterHotKey(handle,hotkeyId,prefs.Modifiers|0x4000,prefs.Key);
   if(!registered) {status.Text="快捷键被占用，请在设置中修改。";tray.ShowBalloonTip(5000,"DeepSeek Mini","快捷键被占用，可从托盘打开设置。",Forms.ToolTipIcon.Warning);}
   Closing+=(s,e)=>{if(!quitting){e.Cancel=true;HidePanel();}};
   SizeChanged+=(s,e)=>{if(WindowState==WindowState.Normal){prefs.Width=Width;prefs.Height=Height;}};
   Microsoft.Win32.SystemEvents.SessionEnding+=(s,e)=>Save();
   SetupFeatures();
   if(!background)ShowPanel();
   else if(prefs.Preload)Dispatcher.BeginInvoke(new Action(Initialize),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   Log("PERF shell_ready_ms="+Program.Clock.ElapsedMilliseconds+" background="+background);
  }
  static SolidColorBrush Brush(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
  static void ApplyCorners(IntPtr h){try{int dark=1,corner=2,border=0x3B3635;Native.DwmSetWindowAttribute(h,20,ref dark,4);Native.DwmSetWindowAttribute(h,33,ref corner,4);Native.DwmSetWindowAttribute(h,34,ref border,4);}catch{}}
  static void ApplyTheme(Window w,bool resize){
   w.FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");w.FontSize=13;w.Foreground=Brush("#EFF0F5");w.Background=Brush("#202126");w.UseLayoutRounding=true;
   WindowChrome.SetWindowChrome(w,new WindowChrome{CaptionHeight=48,ResizeBorderThickness=new Thickness(resize?6:0),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(8),UseAeroCaptionButtons=false});
   w.Resources=(ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='Button'><Setter Property='Foreground' Value='#DADCE5'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Surface' Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#3A3C44'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Background' Value='#484B57'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='#90ADFF'/><Setter TargetName='Surface' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
    <Style TargetType='CheckBox'><Setter Property='Foreground' Value='#E4E5EB'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
   </ResourceDictionary>");
  }
  static Button AddButton(Panel p,string label,Action click) {var b=new Button{Content=label,Padding=new Thickness(14,9,14,9),Margin=new Thickness(0,6,0,0),MinHeight=36};b.Click+=(s,e)=>click();p.Children.Add(b);return b;}
  static Button IconButton(Panel p,string glyph,string label,Action click){var b=AddButton(p,glyph,click);b.FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");b.FontSize=13;b.Width=32;b.Height=32;b.MinHeight=32;b.Padding=new Thickness(0);b.Margin=new Thickness(1,0,1,0);b.ToolTip=label;AutomationProperties.SetName(b,label);WindowChrome.SetIsHitTestVisibleInChrome(b,true);return b;}
  string HotkeyText(){return ((prefs.Modifiers&2)!=0?"Ctrl+":"")+((prefs.Modifiers&1)!=0?"Alt+":"")+((prefs.Modifiers&4)!=0?"Shift+":"")+((Forms.Keys)prefs.Key).ToString();}
  void SetStatus(string message){status.Text=message+" · "+(registered?HotkeyText():"快捷键不可用，请打开设置");}
  async void Initialize() {
   if(starting||initialized||quitting)return;starting=true;SetStatus("正在打开官方网页…");
   try {
    if(web==null){web=new WebView2();web.DefaultBackgroundColor=System.Drawing.Color.FromArgb(20,20,20);webHost.Children.Add(web);}
    var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Data,"WebView2"));
    if(quitting)return;
    await web.EnsureCoreWebView2Async(env);
    if(quitting)return;
    initialized=true;
    webHost.Children[0].Visibility=Visibility.Collapsed;
    AttachWebFeatures();
    web.CoreWebView2.NavigationStarting+=(s,e)=>{Uri u;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out u)||u.Scheme!="https"){e.Cancel=true;SetStatus("已阻止非 HTTPS 跳转");return;}SetStatus("正在加载…");};
    web.CoreWebView2.NavigationCompleted+=(s,e)=>{SetStatus(e.IsSuccess?"DeepSeek 网页版":"网页加载失败："+e.WebErrorStatus+"，可从托盘刷新");Log("PERF navigation_ms="+Program.Clock.ElapsedMilliseconds+" success="+e.IsSuccess);if(e.IsSuccess){FocusChatInput();if(sidebarRequested)ExpandSidebar();}};
    web.CoreWebView2.NewWindowRequested+=(s,e)=>{
     e.Handled=true;
     Uri u;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out u)&&u.Scheme=="https") {
      if(u.Host=="deepseek.com"||u.Host.EndsWith(".deepseek.com",StringComparison.OrdinalIgnoreCase))web.CoreWebView2.Navigate(e.Uri);
      else {modalDepth++;try{if(MessageBox.Show(this,"在系统浏览器打开此链接？\n"+u.Host,"打开外部链接",MessageBoxButton.YesNo)==MessageBoxResult.Yes)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri){UseShellExecute=true});}finally{modalDepth--;}}
     }
    };
    web.CoreWebView2.ProcessFailed+=(s,e)=>{SetStatus("网页进程异常，请退出后重新打开程序");Log("WebView process failed: "+e.ProcessFailedKind);};
    web.CoreWebView2.Navigate("https://chat.deepseek.com/");
   } catch(Exception ex) {SetStatus("网页初始化失败，可从托盘刷新或退出后重试");Log(ex.ToString());if(!initialized&&web!=null){webHost.Children.Remove(web);web.Dispose();web=null;}}
   finally {starting=false;}
  }
  IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {
   if(msg==0x312&&w.ToInt32()==hotkeyId){if(settings!=null)settings.Activate();else if(draftWindow!=null)draftWindow.Activate();else if(IsVisible)HidePanel();else ShowPanel();handled=true;}
   else if(msg==0x312&&w.ToInt32()==ClipboardHotkeyId){OpenDraft(true);handled=true;}
   else if((uint)msg==Native.ShowMessage){ShowPanel();handled=true;}
   return IntPtr.Zero;
  }
  void ShowPanel() {
   if(settings!=null){settings.Activate();return;}
   if(draftWindow!=null){draftWindow.Activate();return;}
   var showClock=System.Diagnostics.Stopwatch.StartNew();
   if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
   var area=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
   var transform=source.CompositionTarget.TransformFromDevice;
   var tl=transform.Transform(new Point(area.Left,area.Top));var br=transform.Transform(new Point(area.Right,area.Bottom));
   if(WindowState==WindowState.Normal&&!IsVisible){
    var remembered=FindRememberedArea();if(remembered.HasValue){tl=remembered.Value.TopLeft;br=remembered.Value.BottomRight;}
    Width=Math.Min(Width,br.X-tl.X);Height=Math.Min(Height,br.Y-tl.Y);
    Left=remembered.HasValue?Math.Max(tl.X,Math.Min(prefs.Left,br.X-Width)):tl.X+Math.Max(0,(br.X-tl.X-Width)/2);
    Top=remembered.HasValue?Math.Max(tl.Y,Math.Min(prefs.Top,br.Y-Height)):tl.Y+Math.Max(0,(br.Y-tl.Y-Height)/2);
   }
   SetMemoryTarget(false);Show();Activate();
   focusRequested=true;
   Dispatcher.BeginInvoke(new Action(()=>{Log("PERF show_dispatch_ms="+showClock.ElapsedMilliseconds);Initialize();FocusChatInput();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
  }
  void Save(){try {var temp=Config+".tmp";using(var s=File.Create(temp))new XmlSerializer(typeof(Preferences)).Serialize(s,prefs);if(File.Exists(Config))File.Replace(temp,Config,null);else File.Move(temp,Config);}catch(Exception ex){Log(ex.Message);SetStatus("设置保存失败，请检查目录写入权限");}}
  static string StartupPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"DeepSeekMini.lnk");}}
  void SetStartup(bool enabled) {
   if(enabled){Type t=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(t);dynamic link=shell.CreateShortcut(StartupPath);link.TargetPath=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;link.Arguments="--background";link.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;link.Save();Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
   else if(File.Exists(StartupPath))File.Delete(StartupPath);
  }
  void OpenSettings() {
   if(settings!=null){settings.Activate();return;}
   if(draftWindow!=null){draftWindow.Activate();return;}
   ShowPanel();settings=new Window{Title="DeepSeek Mini 设置",Icon=Icon,Owner=this,Width=430,Height=600,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};ApplyTheme(settings,false);
   var shell=new DockPanel();settings.Content=new Border{BorderBrush=Brush("#35363B"),BorderThickness=new Thickness(1),Child=shell};
   var heading=new DockPanel{Height=52,Margin=new Thickness(20,0,10,0)};DockPanel.SetDock(heading,Dock.Top);shell.Children.Add(heading);
   var closeRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(closeRow,Dock.Right);heading.Children.Add(closeRow);IconButton(closeRow,"\uE8BB","关闭设置",()=>settings.Close());
   heading.Children.Add(new TextBlock{Text="设置",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
   var p=new StackPanel{Margin=new Thickness(24,14,24,20)};shell.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=p});
   p.Children.Add(new TextBlock{Text="全局快捷键",FontWeight=FontWeights.SemiBold,FontSize=14});
   var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,12)};p.Children.Add(row);
   var ctrl=new CheckBox{Content="Ctrl",IsChecked=(prefs.Modifiers&2)!=0,Margin=new Thickness(0,0,14,0)};row.Children.Add(ctrl);
   var alt=new CheckBox{Content="Alt",IsChecked=(prefs.Modifiers&1)!=0,Margin=new Thickness(0,0,14,0)};row.Children.Add(alt);
   var shift=new CheckBox{Content="Shift",IsChecked=(prefs.Modifiers&4)!=0};row.Children.Add(shift);
   var keys=new ComboBox{Margin=new Thickness(0,0,0,20),Padding=new Thickness(10,7,10,7),Foreground=Brush("#202126")};
   keys.Items.Add("Space");for(char c='A';c<='Z';c++)keys.Items.Add(c.ToString());for(int i=1;i<=12;i++)keys.Items.Add("F"+i);
   keys.SelectedItem=((Forms.Keys)prefs.Key).ToString();if(keys.SelectedIndex<0)keys.SelectedIndex=4;p.Children.Add(keys);
   var startup=new CheckBox{Content="登录 Windows 时启动（隐藏到托盘）",IsChecked=File.Exists(StartupPath),Margin=new Thickness(0,0,0,12)};p.Children.Add(startup);
   var remember=Option(p,"记住窗口位置",prefs.RememberPosition);
   var escape=Option(p,"Esc 隐藏窗口",prefs.EscapeHides);
   var outside=Option(p,"点击其他窗口时隐藏（置顶时不隐藏）",prefs.HideOnDeactivate);
   var memory=Option(p,"隐藏时优先节省内存（恢复可能稍慢）",prefs.MemorySaver);
   var preload=Option(p,"后台启动时预加载网页（占用更多内存）",prefs.Preload);
   p.Children.Add(new TextBlock{Text="Ctrl+Alt+Q：读取剪贴板并预览草稿。\n默认后台启动不加载网页，首次呼出后保持就绪。",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#A1A5B2"),FontSize=12,Margin=new Thickness(0,5,0,12)});
   var save=AddButton(p,"保存设置",()=>{
    uint mods=(ctrl.IsChecked==true?2u:0)|(alt.IsChecked==true?1u:0)|(shift.IsChecked==true?4u:0);
    if(mods==0){MessageBox.Show(settings,"请至少选择一个组合键。");return;}
    uint key=(uint)(Forms.Keys)Enum.Parse(typeof(Forms.Keys),(string)keys.SelectedItem);
    bool changed=mods!=prefs.Modifiers||key!=prefs.Key||!registered;
    int next=hotkeyId==1?2:1;
    if(changed&&!Native.RegisterHotKey(handle,next,mods|0x4000,key)){MessageBox.Show(settings,"该快捷键已被占用，原设置保持不变。");return;}
    try{SetStartup(startup.IsChecked==true);}catch(Exception ex){if(changed)Native.UnregisterHotKey(handle,next);MessageBox.Show(settings,"开机启动设置失败："+ex.Message);return;}
    if(changed){Native.UnregisterHotKey(handle,hotkeyId);hotkeyId=next;registered=true;prefs.Modifiers=mods;prefs.Key=key;}
    prefs.RememberPosition=remember.IsChecked==true;prefs.EscapeHides=escape.IsChecked==true;prefs.HideOnDeactivate=outside.IsChecked==true;prefs.MemorySaver=memory.IsChecked==true;prefs.Preload=preload.IsChecked==true;
    Save();SetStatus("设置已保存");settings.Close();
   });
   save.Background=Brush("#4B6BFB");save.Foreground=Brushes.White;
   settings.SourceInitialized+=(s,e)=>ApplyCorners(new WindowInteropHelper(settings).Handle);
   settings.Closed+=(s,e)=>settings=null;settings.ShowDialog();
  }
  void Quit(){quitting=true;RememberBounds();Save();Native.UnregisterHotKey(handle,hotkeyId);Native.UnregisterHotKey(handle,ClipboardHotkeyId);tray.Visible=false;tray.Dispose();if(whaleIcon!=null)whaleIcon.Dispose();if(settings!=null)settings.Close();if(draftWindow!=null)draftWindow.Close();if(web!=null)web.Dispose();Close();Application.Current.Shutdown();}
 }
}
