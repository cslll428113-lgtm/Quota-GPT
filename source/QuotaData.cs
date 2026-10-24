using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace QuotaNext;
internal sealed record QuotaWindow(string Label,double Remaining,long? ResetsAt,int? Minutes);
internal sealed record QuotaSnapshot(List<QuotaWindow> Windows,DateTimeOffset At);
internal static class QuotaData
{
 internal static QuotaSnapshot Parse(JsonElement result)
 {
  JsonElement bucket;
  if(result.TryGetProperty("rateLimitsByLimitId",out var map) && map.ValueKind==JsonValueKind.Object && map.TryGetProperty("codex",out var specific))bucket=specific;
  else if(result.TryGetProperty("rateLimits",out var legacy)&&legacy.ValueKind==JsonValueKind.Object) {
   if(legacy.TryGetProperty("limitId",out var key)&&key.ValueKind==JsonValueKind.String&&key.GetString()!="codex")throw new Exception("账号未返回 Work / Codex 套餐额度。");
   bucket=legacy;
  } else throw new Exception("账号未返回可识别的套餐额度。");
  var windows=new List<QuotaWindow>();
  foreach(string name in new[]{"primary","secondary"}) {
   if(!bucket.TryGetProperty(name,out var w)||w.ValueKind!=JsonValueKind.Object)continue;
   if(!w.TryGetProperty("usedPercent",out var used)||!used.TryGetDouble(out double pct)||!double.IsFinite(pct)||pct<0||pct>100)throw new Exception("返回的额度数值异常，暂不更新。");
   int? minutes=w.TryGetProperty("windowDurationMins",out var duration)&&duration.TryGetInt32(out int mins)&&mins>0?mins:null;
   long? reset=w.TryGetProperty("resetsAt",out var r)&&r.TryGetInt64(out long seconds)&&seconds>0&&seconds<253402300799?seconds:null;
   string label=minutes switch {10080=>"每周额度",300=>"5 小时额度",1440=>"每日额度",int n when n%60==0=>$"{n/60} 小时额度",int n=>$"{n} 分钟额度",_=>name=="primary"?"主额度":"次额度"};
   windows.Add(new(label,100-pct,reset,minutes));
  }
  if(windows.Count==0)throw new Exception("当前账号没有可显示的额度窗口。");
  // The most constrained window is the large card. Labels always follow actual windows.
  return new(windows.OrderBy(w=>w.Remaining).ToList(),DateTimeOffset.Now);
 }
 internal static string ResetText(QuotaWindow value,DateTimeOffset now) {
  if(value.ResetsAt==null)return "暂未提供重置时间";
  var d=DateTimeOffset.FromUnixTimeSeconds(value.ResetsAt.Value)-now;
  if(d<=TimeSpan.Zero)return "等待服务器确认重置";
  return d.TotalDays>=1?$"{(int)d.TotalDays} 天 {d.Hours} 小时后重置":d.TotalHours>=1?$"{(int)d.TotalHours} 小时 {d.Minutes} 分钟后重置":$"{Math.Max(1,(int)Math.Ceiling(d.TotalMinutes))} 分钟后重置";
 }
}
