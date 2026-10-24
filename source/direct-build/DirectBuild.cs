#if DIRECT_BUILD
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Media;
[assembly: AssemblyVersion("0.6.1.0")]
[assembly: AssemblyFileVersion("0.6.1.0")]
namespace QuotaNext;
internal static class DirectEntry {
 [STAThread] static void Main(){var app=new App();using var r=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaNext.Resources.xaml")!;app.Resources=(ResourceDictionary)XamlReader.Load(r);app.Run();}
}
public partial class MainWindow {
 internal Border NormalSurface=null!;
 internal Button MenuButton=null!;
 internal Button DirectNode2=null!;
 internal TextBlock ScopeLabel=null!;
 internal TextBlock PlanLabel=null!;
 internal StackPanel Onboarding=null!;
 internal Button ConnectButton=null!;
 internal StackPanel ReadingPanel=null!;
 internal TextBlock MainLabel=null!;
 internal TextBlock MainNumber=null!;
 internal Grid MainTrack=null!;
 internal Border MainBar=null!;
 internal TextBlock MainReset=null!;
 internal Border SecondCard=null!;
 internal TextBlock SecondLabel=null!;
 internal TextBlock SecondReset=null!;
 internal TextBlock SecondNumber=null!;
 internal Ellipse StatusDot=null!;
 internal TextBlock StatusLabel=null!;
 internal TextBlock TimeLabel=null!;
 internal Button RefreshButton=null!;
 internal Border DockPill=null!;
 internal TextBlock DockNumber=null!;
 internal Popup ActionPopup=null!;
 internal StackPanel ActionItems=null!;
 void InitializeComponent(){
 Width=272; SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");UseLayoutRounding=true;Foreground=new SolidColorBrush(Color.FromRgb(236,240,245));Title="Quota · GPT 额度";
 using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaNext.Content.xaml")!;
 var root=(Grid)XamlReader.Load(s);
 NormalSurface=(Border)root.FindName("NormalSurface");
 MenuButton=(Button)root.FindName("MenuButton");
 DirectNode2=(Button)root.FindName("DirectNode2");
 ScopeLabel=(TextBlock)root.FindName("ScopeLabel");
 PlanLabel=(TextBlock)root.FindName("PlanLabel");
 Onboarding=(StackPanel)root.FindName("Onboarding");
 ConnectButton=(Button)root.FindName("ConnectButton");
 ReadingPanel=(StackPanel)root.FindName("ReadingPanel");
 MainLabel=(TextBlock)root.FindName("MainLabel");
 MainNumber=(TextBlock)root.FindName("MainNumber");
 MainTrack=(Grid)root.FindName("MainTrack");
 MainBar=(Border)root.FindName("MainBar");
 MainReset=(TextBlock)root.FindName("MainReset");
 SecondCard=(Border)root.FindName("SecondCard");
 SecondLabel=(TextBlock)root.FindName("SecondLabel");
 SecondReset=(TextBlock)root.FindName("SecondReset");
 SecondNumber=(TextBlock)root.FindName("SecondNumber");
 StatusDot=(Ellipse)root.FindName("StatusDot");
 StatusLabel=(TextBlock)root.FindName("StatusLabel");
 TimeLabel=(TextBlock)root.FindName("TimeLabel");
 RefreshButton=(Button)root.FindName("RefreshButton");
 DockPill=(Border)root.FindName("DockPill");
 DockNumber=(TextBlock)root.FindName("DockNumber");
 ActionPopup=(Popup)root.FindName("ActionPopup");
 ActionItems=(StackPanel)root.FindName("ActionItems");
 PreviewMouseLeftButtonDown += PointerDown;
 PreviewMouseMove += PointerMove;
 PreviewMouseLeftButtonUp += PointerUp;
 MouseEnter += PointerEnter;
 MouseLeave += PointerLeave;
MenuButton.Click += ShowMenu;
DirectNode2.Click += HideClick;
ConnectButton.Click += ConnectClick;
MainTrack.SizeChanged += TrackSizeChanged;
RefreshButton.Click += RefreshClick;
DockPill.MouseRightButtonUp += DockMenu;
ActionPopup.Closed += MenuClosed;
 Content=root;
 }
}
#endif
