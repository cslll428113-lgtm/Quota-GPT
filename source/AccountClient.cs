using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace QuotaNext;
internal sealed class AccountClient : IDisposable
{
 readonly ConcurrentDictionary<int,TaskCompletionSource<JsonElement>> pending=new();
 readonly SemaphoreSlim writer=new(1),startup=new(1);
 Process? process; int serial; bool ready,disposed;
 internal event Action<string,JsonElement>? Notification;
 internal event Action? Disconnected;
 internal async Task EnsureStarted()
 {
  await startup.WaitAsync();
  try {
   if(disposed)throw new ObjectDisposedException(nameof(AccountClient));
   if(ready && process is {HasExited:false})return;
   // A self-extracting bundle can use a runtime directory distinct from the host exe.
   var appDirectory=Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
   const string engineName="codex-app-server-x86_64-pc-windows-msvc.exe";
   var engine=Path.Combine(appDirectory,"engine",engineName);
   if(!File.Exists(engine)) {
    var bundledEngine=Path.Combine(AppContext.BaseDirectory,"engine",engineName);
    if(File.Exists(bundledEngine))engine=bundledEngine;
    else throw new Exception("找不到官方组件："+engine+"。请确认 engine 文件夹与 Quota.exe 放在一起；若文件被安全软件隔离，请先检查隔离记录。");
   }
   using(var stream=File.OpenRead(engine)) {
    var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));
    if(!hash.Equals(EngineHash.Value,StringComparison.OrdinalIgnoreCase))throw new Exception("官方组件校验失败，请重新解压原始下载包。");
   }
   process?.Dispose();
   var info=new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,WorkingDirectory=appDirectory};
   info.ArgumentList.Add("--listen");info.ArgumentList.Add("stdio://");
   // No inference calls, no API keys, no credential-file parsing.
   process=new Process{StartInfo=info};process.Start();
   var current=process;
   _=DrainErrors(current);_=ReadLoop(current);
   await Call("initialize",new{clientInfo=new{name="quota_desktop",title="Quota Desktop",version="0.6.1"}});
   await Send(new{method="initialized",@params=new{}});
   ready=true;
  } finally {startup.Release();}
 }
 async Task DrainErrors(Process source){try{while(await source.StandardError.ReadLineAsync()!=null){}}catch{} }
 async Task ReadLoop(Process source)
 {
  try {
   while(await source.StandardOutput.ReadLineAsync() is string line) {
    using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
    if(root.TryGetProperty("id",out var id) && id.TryGetInt32(out int n) && pending.TryRemove(n,out var completion)) {
     if(root.TryGetProperty("error",out var err)) {
      string message=err.TryGetProperty("message",out var m)?m.GetString()??"请求失败":"请求失败";
      completion.TrySetException(new Exception(message));
     } else if(root.TryGetProperty("result",out var result))completion.TrySetResult(result.Clone());
     else completion.TrySetException(new Exception("组件返回了无法识别的响应。"));
    } else if(root.TryGetProperty("method",out var method)) {
     // Never accept server-initiated action requests; this client only reads account limits.
     if(root.TryGetProperty("id",out var requestId))
      await Send(new{id=requestId.Clone(),error=new{code=-32601,message="This client supports read-only account monitoring only."}});
     else if(root.TryGetProperty("params",out var args))Notification?.Invoke(method.GetString()??"",args.Clone());
    }
   }
  } catch { }
  finally {
   if(ReferenceEquals(process,source)) {
    ready=false;
    foreach(var item in pending)if(pending.TryRemove(item.Key,out var c))c.TrySetException(new Exception("官方组件连接中断，请点击重试。"));
    if(!disposed)Disconnected?.Invoke();
   }
  }
 }
 internal async Task<JsonElement> Call(string method,object? args=null)
 {
  if(method is not ("initialize" or "account/read" or "account/login/start" or "account/login/cancel" or "account/rateLimits/read"))throw new InvalidOperationException("不允许此操作。");
  int id=Interlocked.Increment(ref serial);
  var completion=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);pending[id]=completion;
  try {await Send(new{id,method,@params=args??new{}});return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));}
  catch(TimeoutException){throw new Exception("连接超时，请检查网络后重试。");}
  finally {pending.TryRemove(id,out _);}
 }
 async Task Send(object value)
 {
  await writer.WaitAsync();
  try {
   if(process==null || process.HasExited)throw new Exception("官方组件未启动，请重试。");
   await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value));await process.StandardInput.FlushAsync();
  } finally {writer.Release();}
 }
 public void Dispose(){disposed=true;ready=false;try{if(process is {HasExited:false})process.Kill(true);}catch{}process?.Dispose();}
}
