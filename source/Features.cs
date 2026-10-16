using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using Forms=System.Windows.Forms;

namespace DeepSeekMini {
 public partial class MainWindow {
  const int ClipboardHotkeyId=20;
  readonly Grid webHost=new Grid();
  Window draftWindow;
  bool clipboardRegistered,firstFrame;
  int modalDepth;
  int focusEpoch;
  bool focusRequested;
  bool sidebarRequested;
  int sidebarEpoch;
  async void ExpandSidebar(){
   if(!initialized||!sidebarRequested)return;
   int epoch=++sidebarEpoch;bool clicked=false;
   await System.Threading.Tasks.Task.Delay(200);
   for(int attempt=0;attempt<20;attempt++){
    if(quitting||epoch!=sidebarEpoch||!sidebarRequested||WindowState!=WindowState.Maximized||!IsVisible)return;
    try{
     // The native sidebar icon is identified by its SVG shape, never by chat text.
     // Its position moves from the compact header to the sidebar's trailing edge.
     string result=await web.ExecuteScriptAsync(@"(() => {
      if(location.protocol!=='https:'||location.hostname!=='chat.deepseek.com')return 'skip';
      if([...document.querySelectorAll('[role=dialog],[aria-modal=true]')].some(e=>e.getClientRects().length>0))return 'skip';
      const matches=[...document.querySelectorAll('button,[role=button]')].filter(e=>{
       const r=e.getBoundingClientRect();
       return r.width>0&&r.x>=0&&r.x<400&&r.y>=0&&r.y<100&&
        [...e.querySelectorAll('svg path')].some(p=>(p.getAttribute('d')||'').startsWith('M9.67272 0.522841'));
      });
      if(matches.length!==1)return 'wait';
      const toggle=matches[0],r=toggle.getBoundingClientRect();
      if(r.x>120)return 'open';
      if(!__ALLOW_CLICK__)return 'wait';
      toggle.click();return 'clicked';
     })()".Replace("__ALLOW_CLICK__",clicked?"false":"true"));
     if(epoch!=sidebarEpoch)return;
     if(result=="\"clicked\"")clicked=true;
     if(result=="\"open\""){sidebarRequested=false;Log("Sidebar expanded / already open");return;}
     if(result=="\"skip\""){sidebarRequested=false;return;}
    }catch{if(quitting)return;}
    await System.Threading.Tasks.Task.Delay(200);
   }
   if(epoch==sidebarEpoch){sidebarRequested=false;SetStatus("边栏未能自动展开，可点击网页左上角边栏按钮");}
  }
  void CancelInputFocus(){focusRequested=false;focusEpoch++;}
  async void FocusChatInput(){
   if(!focusRequested||!initialized)return;
   int epoch=++focusEpoch;
   const string script=@"(() => {
    if(location.protocol!=='https:'||location.hostname!=='chat.deepseek.com')return 'skip';
    const visible=e=>e.getClientRects().length>0&&getComputedStyle(e).visibility!=='hidden';
    if([...document.querySelectorAll('[role=dialog],[aria-modal=true],dialog[open]')].some(visible))return 'skip';
    const candidates=[...document.querySelectorAll('textarea')].filter(e=>/DeepSeek/i.test(e.placeholder||'')&&!e.disabled&&!e.readOnly&&visible(e));
    if(candidates.length!==1)return 'wait';
    const input=candidates[0],active=document.activeElement;
    if(active&&active!==input&&(active.matches('input,select,textarea')||active.isContentEditable))return 'skip';
    const start=input.selectionStart,end=input.selectionEnd,direction=input.selectionDirection;
    input.focus({preventScroll:true});
    input.setSelectionRange(start,end,direction);
    return document.activeElement===input?'ok':'wait';
   })()";
   for(int attempt=0;attempt<25;attempt++){
    if(epoch!=focusEpoch||!focusRequested||!IsVisible||!IsActive||quitting||settings!=null||draftWindow!=null||modalDepth>0)return;
    try{
     string result=await web.ExecuteScriptAsync(script);
     if(epoch!=focusEpoch||!IsVisible||!IsActive||settings!=null||draftWindow!=null)return;
     if(result=="\"ok\""){web.Focus();focusRequested=false;Log("Input focus restored");return;}
     if(result=="\"skip\""){CancelInputFocus();return;}
    }catch{if(quitting)return;}
    await System.Threading.Tasks.Task.Delay(200);
   }
   if(epoch==focusEpoch){focusRequested=false;SetStatus("暂未找到聊天输入框，请点击输入框继续");}
  }
  static CheckBox Option(Panel p,string label,bool value){var c=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,0,0,12)};p.Children.Add(c);return c;}
  void SetupFeatures(){
   Deactivated+=(s,e)=>CancelInputFocus();
   PreviewMouseDown+=(s,e)=>CancelInputFocus();
   PreviewKeyDown+=(s,e)=>{if(e.Key!=Key.LeftAlt&&e.Key!=Key.RightAlt&&e.Key!=Key.LeftCtrl&&e.Key!=Key.RightCtrl)CancelInputFocus();};
   clipboardRegistered=Native.RegisterHotKey(handle,ClipboardHotkeyId,3|0x4000,81);
   if(!clipboardRegistered){Log("Clipboard shortcut unavailable");tray.ShowBalloonTip(5000,"DeepSeek Mini","Ctrl+Alt+Q 已被占用，可点击标题栏的快捷草稿按钮。",Forms.ToolTipIcon.Warning);}
   ContentRendered+=(s,e)=>{if(!firstFrame){firstFrame=true;Log("PERF first_content_rendered_ms="+Program.Clock.ElapsedMilliseconds);}};
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape&&Keyboard.Modifiers==ModifierKeys.None&&prefs.EscapeHides&&settings==null&&draftWindow==null&&modalDepth==0){e.Handled=true;HidePanel();}};
   Deactivated+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{if(!quitting&&prefs.HideOnDeactivate&&!Topmost&&!IsActive&&settings==null&&draftWindow==null&&modalDepth==0&&IsVisible)HidePanel();}),DispatcherPriority.Background);
  }
  void AttachWebFeatures(){
   web.KeyDown+=(s,e)=>{if(e.Key==Key.Escape&&Keyboard.Modifiers==ModifierKeys.None&&prefs.EscapeHides&&settings==null&&draftWindow==null&&modalDepth==0){e.Handled=true;Dispatcher.BeginInvoke(new Action(HidePanel));}};
   SetMemoryTarget(!IsVisible);
   web.CoreWebView2.DOMContentLoaded+=(s,e)=>Log("PERF dom_loaded_ms="+Program.Clock.ElapsedMilliseconds);
  }
  void SetMemoryTarget(bool hidden){
   if(!initialized||web==null)return;
   try{web.CoreWebView2.MemoryUsageTargetLevel=hidden&&prefs.MemorySaver?CoreWebView2MemoryUsageTargetLevel.Low:CoreWebView2MemoryUsageTargetLevel.Normal;}
   catch(Exception ex){Log("Memory target: "+ex.Message);}
  }
  void RememberBounds(){if(IsVisible&&WindowState==WindowState.Normal){prefs.Left=Left;prefs.Top=Top;prefs.Width=Width;prefs.Height=Height;}}
  Rect? FindRememberedArea(){
   if(!prefs.RememberPosition||double.IsNaN(prefs.Left)||double.IsNaN(prefs.Top)||double.IsInfinity(prefs.Left)||double.IsInfinity(prefs.Top))return null;
   var transform=source.CompositionTarget.TransformFromDevice;
   foreach(var screen in Forms.Screen.AllScreens){var a=screen.WorkingArea;var top=transform.Transform(new Point(a.Left,a.Top));var bottom=transform.Transform(new Point(a.Right,a.Bottom));var rect=new Rect(top,bottom);if(rect.Contains(new Point(prefs.Left+40,prefs.Top+20)))return rect;}
   return null;
  }
  void HidePanel(){if(quitting)return;CancelInputFocus();RememberBounds();Save();Hide();SetMemoryTarget(true);}
  string ReadClipboard(){
   try{if(!Clipboard.ContainsText()){SetStatus("剪贴板里没有文字，可在草稿中输入");return "";}var text=Clipboard.GetText();if(text.Length>20000){SetStatus("剪贴板超过 20,000 字符，请先缩短后再导入");return "";}return text;}
   catch{SetStatus("剪贴板暂时不可用，请稍后再导入");return "";}
  }
  static TextBox Editor(int max){return new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brush("#17181C"),Foreground=Brush("#EFF0F5"),BorderBrush=Brush("#393C46"),CaretBrush=Brushes.White,Padding=new Thickness(10),MaxLength=max,FontSize=13};}
  static readonly string[] PromptNames={"直接提问","翻译","润色","总结","解释代码"};
  static readonly string[] Instructions={"","请将下面的文字翻译成自然、准确的中文；如果原文是中文，请翻译成英文。只输出译文。","请润色下面的文字，保留原意和事实，让表达更清晰自然。只输出润色后的版本。","请总结下面的内容：先给出一句话结论，再列出关键要点；不要补充原文没有的信息。","请解释下面的代码：说明用途、关键步骤、输入输出，以及值得注意的问题。"};
  void OpenDraft(bool fromClipboard){
   if(settings!=null){settings.Activate();return;}
   if(draftWindow!=null){draftWindow.Activate();return;}
   string captured=fromClipboard?ReadClipboard():"";
   ShowPanel();
   draftWindow=new Window{Title="快捷草稿",Icon=Icon,Owner=this,Width=550,Height=640,MinWidth=420,MinHeight=520,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=true};
   ApplyTheme(draftWindow,true);
   var shell=new DockPanel();draftWindow.Content=new Border{BorderBrush=Brush("#35363B"),BorderThickness=new Thickness(1),Child=shell};
   var heading=new DockPanel{Height=52,Margin=new Thickness(18,0,10,0)};DockPanel.SetDock(heading,Dock.Top);shell.Children.Add(heading);
   var closeRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(closeRow,Dock.Right);heading.Children.Add(closeRow);IconButton(closeRow,"\uE8BB","关闭草稿",()=>draftWindow.Close());
   heading.Children.Add(new TextBlock{Text="快捷草稿",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
   var p=new Grid{Margin=new Thickness(22,8,22,18)};shell.Children.Add(p);
   p.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   p.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   p.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   p.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   p.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   p.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   p.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var tools=new DockPanel{Margin=new Thickness(0,0,0,12)};p.Children.Add(tools);
   var importRow=new StackPanel();DockPanel.SetDock(importRow,Dock.Right);tools.Children.Add(importRow);
   var kind=new ComboBox{ItemsSource=PromptNames,SelectedIndex=0,Padding=new Thickness(8),Foreground=Brush("#202126"),Margin=new Thickness(0,6,10,0)};tools.Children.Add(kind);
   var label=new TextBlock{Text="原文 / 问题（最多 20,000 字符）",Foreground=Brush("#A1A5B2"),Margin=new Thickness(0,0,0,7)};Grid.SetRow(label,1);p.Children.Add(label);
   var input=Editor(20000);input.Text=captured;Grid.SetRow(input,2);p.Children.Add(input);
   var previewLabel=new TextBlock{Text="草稿预览 · 可直接编辑",Margin=new Thickness(0,14,0,7),Foreground=Brush("#A1A5B2")};Grid.SetRow(previewLabel,3);p.Children.Add(previewLabel);
   var preview=Editor(24000);Grid.SetRow(preview,4);p.Children.Add(preview);
   Action regenerate=()=>{int index=Math.Max(0,kind.SelectedIndex);preview.Text=string.IsNullOrWhiteSpace(input.Text)?"":Instructions[index]+(index==0?"":"\n\n")+input.Text;};
   input.TextChanged+=(s,e)=>regenerate();kind.SelectionChanged+=(s,e)=>regenerate();regenerate();
   AddButton(importRow,"导入剪贴板",()=>{var text=ReadClipboard();if(text.Length>0){input.Text=text;input.Focus();}});
   var feedback=new TextBlock{Text="草稿仅保留在内存中；放入网页后由你手动发送。",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#A1A5B2"),FontSize=12,Margin=new Thickness(0,12,0,6)};Grid.SetRow(feedback,5);p.Children.Add(feedback);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(actions,6);p.Children.Add(actions);
   AddButton(actions,"复制草稿",()=>{try{if(!string.IsNullOrWhiteSpace(preview.Text)){Clipboard.SetText(preview.Text);feedback.Text="已复制，可在网页输入框中粘贴。";}}catch{feedback.Text="剪贴板暂时不可用，请重试。";}});
   var transfer=AddButton(actions,"放入网页",()=>{});transfer.Margin=new Thickness(10,6,0,0);transfer.Background=Brush("#4B6BFB");transfer.Foreground=Brushes.White;
   transfer.Click+=async (s,e)=>{
    if(string.IsNullOrWhiteSpace(preview.Text)){feedback.Text="请先输入或导入文字。";return;}
    if(!initialized){feedback.Text="网页仍在加载，稍后再试，或先复制草稿。";Initialize();return;}
    transfer.IsEnabled=false;
    try{
     string script;using(var reader=new StreamReader(typeof(MainWindow).Assembly.GetManifestResourceStream("FillDraft.js")))script=reader.ReadToEnd();
     var json=new JavaScriptSerializer().Serialize(preview.Text).Replace("\u2028","\\u2028").Replace("\u2029","\\u2029");
     string result=await web.ExecuteScriptAsync(script.Replace("__DRAFT_JSON__",json));
     if(draftWindow==null)return;
     if(result=="\"ok\""){draftWindow.Close();Activate();web.Focus();SetStatus("草稿已填入，请检查后手动发送");}
     else if(result=="\"occupied\"")feedback.Text="网页已有未发送内容，未覆盖。请先处理原内容，或复制草稿。";
     else feedback.Text="未找到可用的官方输入框，请复制草稿后手动粘贴。";
    }catch(Exception ex){Log("Draft transfer failed: "+ex.GetType().Name);if(draftWindow!=null)feedback.Text="自动填入失败，草稿仍在，可复制后手动粘贴。";}
    finally{transfer.IsEnabled=true;}
   };
   draftWindow.SourceInitialized+=(s,e)=>ApplyCorners(new System.Windows.Interop.WindowInteropHelper(draftWindow).Handle);
   draftWindow.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;draftWindow.Close();}};
   draftWindow.Closed+=(s,e)=>draftWindow=null;
   draftWindow.ContentRendered+=(s,e)=>input.Focus();
   draftWindow.ShowDialog();
  }
 }
}
