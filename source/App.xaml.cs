using System;
using System.IO;
using System.Threading;
using System.Windows;
namespace QuotaNext;
public partial class App : System.Windows.Application
{
 internal static readonly string DataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GPTQuotaNext");
 Mutex? mutex;
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  mutex=new Mutex(true,@"Local\GPTQuotaNext",out bool first);
  if(!first){MessageBox.Show("小窗已经运行，请双击右下角托盘图标。","Quota");Shutdown();return;}
  try {Directory.CreateDirectory(DataDir);new MainWindow().Show();}
  catch(Exception ex){MessageBox.Show("启动失败："+ex.Message,"Quota");Shutdown();}
 }
 protected override void OnExit(ExitEventArgs e){mutex?.Dispose();base.OnExit(e);}
}
