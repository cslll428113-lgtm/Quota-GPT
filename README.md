# Quota · GPT 额度悬浮窗

Windows 10/11 x64 桌面工具，当前基线版本为 **0.6.1**。

## 功能
- 浏览器完成账号授权，后台每 60 秒查询 Work / Codex 额度。
- 悬浮窗、系统托盘、拖动、侧边吸附和数值收起。
- 显示剩余额度、重置时间及数据过期状态。

## 当前状态
已交付 Windows 压缩包。升级 Pro 后出现同步失败的反馈仍未定位，不能认为已修复。额度以官方使用情况页为准。本工具不覆盖所有普通聊天、图片和语音额度。

## 安装
下载完整 Windows 发布包，全部解压，运行 Quota.exe。engine 文件夹必须与 exe 放在一起。

## 源码与构建
source 内为 C# / WPF 源码，需要 Windows 和 .NET 8 SDK。
从原始完整包取出 engine 文件夹，放到仓库根目录，然后运行：

```powershell
./build.ps1
```

构建结果位于 dist，包含程序、官方 engine、许可证和说明。脚本检查 engine 的 SHA-256，避免发布缺少组件的更新包。
source/direct-build 保存了早期受限构建环境使用的后备代码，常规构建不启用 DIRECT_BUILD。

## 后续更新方式
以此仓库为源码基线，每次修改提交一个有说明的版本；更新 CHANGELOG.md 和项目版本号，在 Windows 上验证后，将完整包上传至 GitHub Releases。保留旧版便于回退。
GitHub 保存代码和版本记录；当前客户端没有自动下载或安装更新功能。后续可在聊天中要求继续修改并同步该仓库。

## 账号与隐私
不上传登录数据、Cookie、API Key 或个人配置。官方组件自行管理账号授权，可能与本机 Codex 共用登录状态。不要提交 .codex、auth.json 或日志。

## 第三方组件
OpenAI Codex App Server 0.160.0，相关许可见 licenses。源码与完整包是不同交付物；不要把只有源码的 ZIP 当作可直接运行的程序。
