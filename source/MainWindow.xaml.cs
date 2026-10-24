using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;
namespace QuotaNext;
public partial class MainWindow : Window
{
 readonly AccountClient client=new();
 readonly DispatcherTimer minute=new(){Interval=TimeSpan.FromSeconds(60)},clock=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly Forms.NotifyIcon tray;
 QuotaSnapshot? snapshot; bool busy,paused,quitting,compact,alerted,connected; string? loginId;
 DateTimeOffset loginStarted;
 readonly string prefs=Path.Combine(App.DataDir,"window.json");
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
 public MainWindow()
 {
  InitializeComponent();
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaNext.app.ico")!;
  tray=new Forms.NotifyIcon{Icon=new Drawing.Icon(stream),Visible=true,Text="Quota · 等待连接"};
  var menu=new Forms.ContextMenuStrip();
  menu.Items.Add("显示 / 隐藏",null,(_,_)=>Dispatcher.Invoke(()=>{if(IsVisible)Hide();else Reveal();}));
  menu.Items.Add("立即同步",null,async(_,_)=>await Sync());
  menu.Items.Add("退出",null,(_,_)=>Dispatcher.Invoke(Quit));tray.ContextMenuStrip=menu;
  tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Reveal);
  client.Notification+=(method,args)=>Dispatcher.BeginInvoke(new Action(()=>OnNotification(method,args)));
  client.Disconnected+=()=>Dispatcher.BeginInvoke(new Action(()=>{connected=false;Status("连接中断 · 点击右侧重试",true);UpdateClock();}));
  Loaded+=async(_,_)=>{
   RestoreLayout();
   await Sync();minute.Start();clock.Start();
  };
  minute.Tick+=async(_,_)=>{if(!paused&&loginId==null)await Sync();};
  clock.Tick+=(_,_)=>{
   if(loginId!=null&&DateTimeOffset.Now-loginStarted>TimeSpan.FromMinutes(5)){_=CancelLogin();Status("授权等待已结束，请重新连接",true);}
   UpdateClock();
  };
  InitializeDocking();
  Closing+=OnClosing;
 }
 void Status(string text,bool error=false){StatusLabel.Text=text;StatusDot.Fill=new SolidColorBrush((Color)ColorConverter.ConvertFromString(error?"#E8B58E":connected&&!paused?"#A6E7CC":"#778393"));}
 async Task Sync()
 {
  if(busy||quitting||loginId!=null)return;
  busy=true;RefreshButton.IsEnabled=false;
  try {
   Status("正在同步…");await client.EnsureStarted();
   var account=await client.Call("account/read",new{refreshToken=false});
   if(!account.TryGetProperty("account",out var a)||a.ValueKind!=JsonValueKind.Object||!a.TryGetProperty("type",out var kind)||kind.GetString()!="chatgpt") {
    connected=false;snapshot=null;PlanLabel.Text="未连接";ShowReading();Status("先在浏览器完成一次授权");return;
   }
   PlanLabel.Text=a.TryGetProperty("planType",out var plan)&&plan.ValueKind==JsonValueKind.String?(plan.GetString()??"CHATGPT").ToUpperInvariant():"CHATGPT";
   var result=await client.Call("account/rateLimits/read");
   snapshot=QuotaData.Parse(result);connected=true;Status(paused?"自动同步已暂停":"已同步 · 后台监测中");ShowReading();
   double lowest=snapshot.Windows.Min(w=>w.Remaining);
   if(lowest<=20&&!alerted){tray.ShowBalloonTip(5000,"额度提醒",$"{snapshot.Windows[0].Label}剩余 {lowest:0.#}%。",Forms.ToolTipIcon.Warning);alerted=true;}
   if(lowest>20)alerted=false;
  }catch(Exception ex){connected=false;Status("同步失败 · 点击右侧重试",true);StatusLabel.ToolTip=SafeError(ex.Message);if(snapshot==null){Onboarding.Visibility=Visibility.Visible;ConnectButton.Content="连接 / 重试  ↗";}}
  finally{busy=false;RefreshButton.IsEnabled=true;UpdateClock();}
 }
 static string SafeError(string error) {
  // Do not put raw auth URLs or credentials into UI/logs.
  if(error.Contains("http",StringComparison.OrdinalIgnoreCase)||error.Contains("token",StringComparison.OrdinalIgnoreCase))return "网络或授权状态异常，请重新连接；检查网络后重试。";
  return error.Length>300?error[..300]:error;
 }
 async void ConnectClick(object sender,RoutedEventArgs e)=>await Connect();
 async Task Connect()
 {
  if(busy||quitting)return;
  if(loginId!=null){await CancelLogin();Status("已取消授权，可以重新连接");return;}
  busy=true;ConnectButton.IsEnabled=false;snapshot=null;connected=false;alerted=false;ShowReading();
  try {
   Status("正在准备浏览器授权…");await client.EnsureStarted();
   var result=await client.Call("account/login/start",new{type="chatgpt"});
   loginId=result.GetProperty("loginId").GetString();loginStarted=DateTimeOffset.Now;
   var raw=result.GetProperty("authUrl").GetString();
   if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host is not ("auth.openai.com" or "chatgpt.com"))throw new Exception("官方组件返回了无法确认的授权地址。");
   Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});
   ConnectButton.Content="等待授权 · 点击取消";Status("请在浏览器完成授权，可使用 Apple 账号");
  } catch(Exception ex){await CancelLogin();Status("授权未启动 · 请重试",true);System.Windows.MessageBox.Show(this,SafeError(ex.Message),"连接 ChatGPT");}
  finally{busy=false;ConnectButton.IsEnabled=true;}
 }
 async Task CancelLogin() {
  var id=loginId;loginId=null;ConnectButton.Content="连接 ChatGPT  ↗";
  if(id!=null)try{await client.Call("account/login/cancel",new{loginId=id});}catch{}
 }
 async void OnNotification(string method,JsonElement args)
 {
  if(quitting)return;
  if(method=="account/login/completed") {
   string? id=args.TryGetProperty("loginId",out var v)?v.GetString():null;
   if(loginId==null||id!=loginId)return;
   loginId=null;ConnectButton.Content="连接 ChatGPT  ↗";
   if(args.TryGetProperty("success",out var success)&&success.ValueKind==JsonValueKind.True)await Sync();
   else Status("授权未完成，请重新连接",true);
  }
  // Read current account state on the next regular poll. Never merge other quota buckets.
 }
 void ShowReading() {
  Onboarding.Visibility=snapshot==null?Visibility.Visible:Visibility.Collapsed;
  ReadingPanel.Visibility=snapshot==null?Visibility.Collapsed:Visibility.Visible;
  if(snapshot==null){UpdateClock();return;}
  var main=snapshot.Windows[0];MainLabel.Text=main.Label;MainNumber.Text=main.Remaining.ToString("0.#");
  MainNumber.FontSize=compact?38:46;UpdateBar();
  MainNumber.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(main.Remaining<=20?"#EDB59D":"#ECF0F5"));
  MainReset.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
  SecondCard.Visibility=snapshot.Windows.Count>1&&!compact?Visibility.Visible:Visibility.Collapsed;
  if(snapshot.Windows.Count>1){SecondLabel.Text=snapshot.Windows[1].Label;SecondNumber.Text=snapshot.Windows[1].Remaining.ToString("0.#");}
  UpdateClock();
  KeepOnScreen();
 }
 void UpdateClock() {
  if(quitting)return;
  bool stale=snapshot!=null&&DateTimeOffset.Now-snapshot.At>TimeSpan.FromMinutes(3);
  if(snapshot!=null){
   MainReset.Text=QuotaData.ResetText(snapshot.Windows[0],DateTimeOffset.Now);
   if(snapshot.Windows.Count>1)SecondReset.Text=QuotaData.ResetText(snapshot.Windows[1],DateTimeOffset.Now);
   TimeLabel.Text=$"{(stale?"读数已过期":!connected||paused?"上次读数":"更新于")} {snapshot.At:HH:mm:ss}  ·  60 秒同步";
   ReadingPanel.Opacity=stale?0.5:1;
  }else TimeLabel.Text="无需插件 · 无需保留使用情况页";
  string badge=snapshot!=null&&connected&&!paused&&!stale?Math.Floor(snapshot.Windows.Min(w=>w.Remaining)).ToString():"—";
  DockNumber.Text=badge=="—"?"—":snapshot!.Windows.Min(w=>w.Remaining).ToString("0.#")+"%";
  DockPill.ToolTip=badge=="—"?"未连接、暂停或读数已过期 · 单击展开查看":$"{snapshot!.Windows[0].Label}剩余 {DockNumber.Text} · 单击展开 · 拖动移开";
  DockNumber.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(badge=="—"?"#949CAB":snapshot!.Windows.Min(w=>w.Remaining)<=20?"#EDB59D":"#A6E7CC"));
  if((string?)tray.Tag==badge)return;tray.Tag=badge;
  using var bitmap=new Drawing.Bitmap(32,32);using var g=Drawing.Graphics.FromImage(bitmap);g.Clear(Drawing.Color.FromArgb(19,23,30));
  using var font=new Drawing.Font("Segoe UI",badge.Length>2?11:15,Drawing.FontStyle.Bold,Drawing.GraphicsUnit.Pixel);
  using var ink=new Drawing.SolidBrush(Drawing.Color.FromArgb(166,231,204));using var format=new Drawing.StringFormat{Alignment=Drawing.StringAlignment.Center,LineAlignment=Drawing.StringAlignment.Center};
  g.DrawString(badge,font,ink,new Drawing.RectangleF(0,0,32,32),format);
  IntPtr h=bitmap.GetHicon();using var borrowed=Drawing.Icon.FromHandle(h);var icon=(Drawing.Icon)borrowed.Clone();DestroyIcon(h);var old=tray.Icon;tray.Icon=icon;old?.Dispose();
  tray.Text=badge=="—"?"Quota · 未同步 / 暂停 / 过期":$"Quota · 最低剩余 {badge}%";
 }
 async void RefreshClick(object sender,RoutedEventArgs e)=>await Sync();
 void HideClick(object sender,RoutedEventArgs e){ActionPopup.IsOpen=false;Hide();}
 void Reveal(){Show();WindowState=WindowState.Normal;if(isDocked)ExpandDock();KeepOnScreen();Activate();}
 void ShowMenu(object sender,RoutedEventArgs e)=>OpenMenu(MenuButton);
 void DockMenu(object sender,MouseButtonEventArgs e){OpenMenu(DockPill);e.Handled=true;}
 void OpenMenu(UIElement anchor) {
  collapseTimer.Stop();ActionItems.Children.Clear();
  void Item(string name,Action action,bool? check=null){
   var button=new Button{Content=(check.HasValue?(check.Value?"✓  ":"    "):"")+name,
    Background=Brushes.Transparent,Foreground=new SolidColorBrush(Color.FromRgb(236,240,245)),
    HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(11,8,11,8),FontSize=12};
   button.Click+=(_,_)=>{ActionPopup.IsOpen=false;action();};ActionItems.Children.Add(button);
  }
  Item("窗口置顶",()=>{Topmost=!Topmost;SavePrefs();},Topmost);
  Item("精简模式",()=>{compact=!compact;ShowReading();KeepOnScreen();SavePrefs();},compact);
  Item("拖到边缘自动收起",()=>{autoDock=!autoDock;if(!autoDock){ExpandDock();dockEdge=DockEdge.None;}SavePrefs();},autoDock);
  Item(isDocked?"展开详细额度":"收起到屏幕侧边",()=>{if(isDocked)ExpandDock();else DockToNearest();});
  Item(paused?"恢复自动同步":"暂停自动同步",()=>{paused=!paused;Status(paused?"自动同步已暂停":"自动同步已恢复");if(!paused)_=Sync();UpdateClock();});
  ActionItems.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromRgb(53,63,77)),Margin=new Thickness(7,4,7,4)});
  Item("重新授权 / 切换账号",()=>{ExpandDock();dockEdge=DockEdge.None;paused=false;_=Connect();});
  Item("使用说明",()=>System.Windows.MessageBox.Show(this,"按住数字、标题或空白处即可拖动。拖到屏幕左右边缘后松开，会收成仅显示百分比的小条。\n\n单击数值条展开；鼠标离开后自动收回。把数值条拖离边缘可恢复悬浮窗。右键数值条也能打开菜单。\n\n··· 菜单可置顶、精简显示、控制贴边、暂停同步或退出。每分钟查询一次；过期或离线时数值条显示横线。\n\n版本 0.6 · Windows x64。", "Quota"));
  Item("退出",Quit);ActionPopup.PlacementTarget=anchor;ActionPopup.Placement=PlacementMode.Bottom;ActionPopup.IsOpen=true;
 }
 void Quit(){quitting=true;Close();}
 void OnClosing(object? sender,CancelEventArgs e){if(!quitting){e.Cancel=true;Hide();return;}SavePrefs();collapseTimer.Stop();minute.Stop();clock.Stop();client.Dispose();tray.Visible=false;tray.Dispose();System.Windows.Application.Current.Shutdown();}
}
