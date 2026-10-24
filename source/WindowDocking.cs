using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace QuotaNext;
public partial class MainWindow
{
 bool isDocked,autoDock=true,dragging,adjusting; DockEdge dockEdge;
 Point? pressPoint;
 readonly DispatcherTimer collapseTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};
 [StructLayout(LayoutKind.Sequential)] struct WinRect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo{public int Size;public WinRect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out WinRect rect);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
 [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 IntPtr Handle=>new WindowInteropHelper(this).Handle;
 DockBox CurrentBox(){GetWindowRect(Handle,out var r);return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
 DockArea WorkArea(){var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};GetMonitorInfo(MonitorFromWindow(Handle,2),ref info);return new(info.Work.Left,info.Work.Top,info.Work.Right,info.Work.Bottom);}
 void MoveBox(DockBox box)=>SetWindowPos(Handle,IntPtr.Zero,(int)Math.Round(box.X),(int)Math.Round(box.Y),(int)Math.Round(box.Width),(int)Math.Round(box.Height),0x0014);
 void InitializeDocking(){
  collapseTimer.Tick+=(_,_)=>{collapseTimer.Stop();if(dockEdge!=DockEdge.None&&!isDocked&&!dragging&&!IsMouseOver&&!ActionPopup.IsOpen&&Mouse.LeftButton!=MouseButtonState.Pressed)CollapseDock();};
  SizeChanged+=(_,_)=>{if(IsLoaded&&!adjusting&&!dragging)Dispatcher.BeginInvoke(new Action(KeepOnScreen));};
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  Closed+=(_,_)=>Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;
 }
 void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(new Action(KeepOnScreen));
 void TrackSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateBar();
 void UpdateBar(){if(snapshot!=null)MainBar.Width=Math.Max(0,MainTrack.ActualWidth)*snapshot.Windows[0].Remaining/100;}
 static bool IsInteractive(DependencyObject? node){
  while(node!=null){if(node is ButtonBase || node is TextBoxBase || node is System.Windows.Controls.Primitives.Popup)return true;
   node=node is Visual||node is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
  return false;
 }
 void PointerDown(object sender,MouseButtonEventArgs e){
  if(e.ChangedButton!=MouseButton.Left||IsInteractive(e.OriginalSource as DependencyObject))return;
  ActionPopup.IsOpen=false;collapseTimer.Stop();pressPoint=PointToScreen(e.GetPosition(this));CaptureMouse();e.Handled=true;
 }
 void PointerMove(object sender,MouseEventArgs e){
  if(pressPoint is not Point start||dragging)return;
  if(e.LeftButton!=MouseButtonState.Pressed){pressPoint=null;ReleaseMouseCapture();return;}
  var now=PointToScreen(e.GetPosition(this));var dpi=VisualTreeHelper.GetDpi(this);
  if(Math.Abs(now.X-start.X)<SystemParameters.MinimumHorizontalDragDistance*dpi.DpiScaleX&&Math.Abs(now.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance*dpi.DpiScaleY)return;
  pressPoint=null;dragging=true;collapseTimer.Stop();ReleaseMouseCapture();
  try{DragMove();}
  catch(InvalidOperationException){Status("拖动未完成，请重新按住拖动",true);}
  finally{dragging=false;}
  if(quitting)return;
  var box=CurrentBox();dockEdge=autoDock?DockGeometry.Detect(box,WorkArea(),24*VisualTreeHelper.GetDpi(this).DpiScaleX):DockEdge.None;
  if(dockEdge!=DockEdge.None)CollapseDock();else {if(isDocked)ExpandDock();KeepOnScreen();}
  SavePrefs();e.Handled=true;
 }
 void PointerUp(object sender,MouseButtonEventArgs e){
  bool click=pressPoint.HasValue;pressPoint=null;if(IsMouseCaptured)ReleaseMouseCapture();
  if(click&&isDocked){ExpandDock();e.Handled=true;}
 }
 void PointerEnter(object sender,MouseEventArgs e)=>collapseTimer.Stop();
 void PointerLeave(object sender,MouseEventArgs e){if(dockEdge!=DockEdge.None&&!isDocked&&!dragging&&!ActionPopup.IsOpen)collapseTimer.Start();}
 void MenuClosed(object? sender,EventArgs e){if(!IsMouseOver&&dockEdge!=DockEdge.None&&!isDocked)collapseTimer.Start();}
 void DockToNearest(){var box=CurrentBox();var work=WorkArea();dockEdge=Math.Abs(box.X-work.Left)<=Math.Abs(box.X+box.Width-work.Right)?DockEdge.Left:DockEdge.Right;CollapseDock();}
 void CollapseDock(){
  if(dockEdge==DockEdge.None)return;
  collapseTimer.Stop();ActionPopup.IsOpen=false;var anchor=CurrentBox();var work=WorkArea();adjusting=true;
  try{
   isDocked=true;NormalSurface.Visibility=Visibility.Collapsed;DockPill.Visibility=Visibility.Visible;
   DockPill.CornerRadius=dockEdge==DockEdge.Left?new CornerRadius(0,9,9,0):new CornerRadius(9,0,0,9);
   SizeToContent=SizeToContent.Manual;Width=76;Height=34;UpdateLayout();
   var dpi=VisualTreeHelper.GetDpi(this);MoveBox(DockGeometry.Place(anchor,76*dpi.DpiScaleX,34*dpi.DpiScaleY,work,dockEdge));
  }finally{adjusting=false;}UpdateClock();SavePrefs();
 }
 void ExpandDock(){
  if(!isDocked)return;
  var anchor=CurrentBox();var work=WorkArea();adjusting=true;
  try{
   isDocked=false;DockPill.Visibility=Visibility.Collapsed;NormalSurface.Visibility=Visibility.Visible;
   Width=272;Height=double.NaN;SizeToContent=SizeToContent.Height;UpdateLayout();
   var dpi=VisualTreeHelper.GetDpi(this);MoveBox(DockGeometry.Place(anchor,272*dpi.DpiScaleX,NormalSurface.DesiredSize.Height*dpi.DpiScaleY,work,dockEdge));
  }finally{adjusting=false;}ShowReading();SavePrefs();
 }
 void KeepOnScreen(){
  if(!IsLoaded||adjusting||dragging||Handle==IntPtr.Zero)return;
  adjusting=true;
  try{var box=CurrentBox();var placed=DockGeometry.Place(box,box.Width,box.Height,WorkArea(),dockEdge);if(placed!=box)MoveBox(placed);}
  finally{adjusting=false;}
 }
 void RestoreLayout(){
  var work=SystemParameters.WorkArea;Left=work.Right-Width-16;Top=work.Bottom-ActualHeight-16;
  try{
   using var d=JsonDocument.Parse(File.ReadAllText(prefs));var r=d.RootElement;
   if(r.TryGetProperty("top",out var t))Topmost=t.GetBoolean();
   if(r.TryGetProperty("compact",out var c))compact=c.GetBoolean();
   if(r.TryGetProperty("autoDock",out var a))autoDock=a.GetBoolean();
   if(r.TryGetProperty("px",out var x)&&r.TryGetProperty("py",out var y)){
    double px=x.GetDouble(),py=y.GetDouble();if(double.IsFinite(px)&&double.IsFinite(py)){var box=CurrentBox();MoveBox(box with{X=px,Y=py});}
   }
   if(autoDock&&r.TryGetProperty("dock",out var edge)&&Enum.TryParse<DockEdge>(edge.GetString(),out var saved)&&Enum.IsDefined(saved))dockEdge=saved;
  }catch{}
  KeepOnScreen();if(dockEdge!=DockEdge.None)CollapseDock();
 }
 void SavePrefs(){
  if(!IsLoaded||Handle==IntPtr.Zero)return;
  try{var box=CurrentBox();File.WriteAllText(prefs,JsonSerializer.Serialize(new{px=box.X,py=box.Y,top=Topmost,compact,autoDock,dock=dockEdge.ToString()}));}catch{}
 }
}
