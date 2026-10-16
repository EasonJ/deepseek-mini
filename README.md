# DeepSeek Mini 🐳

轻量的 Windows 桌面快捷问答窗口，使用 WebView2 打开 DeepSeek 官方网页。

![Pixel whale](assets/whale.png)

这是独立开发的非官方客户端，与 DeepSeek 无隶属或合作关系。使用官方网页，不需要 API Key。网页服务的登录要求、额度和可用性由 DeepSeek 决定。

## 下载使用

在本仓库 **Releases** 页面下载 `DeepSeekMini-Windows-x64.zip`，解压后双击 `DeepSeekMini.exe`。请保留同目录的 DLL 和配置文件。

运行环境：Windows x64、.NET Framework 4.7.2 或更高、Microsoft Edge WebView2 Runtime。

## 功能

- 全局快捷键呼出、隐藏；托盘常驻。
- Windows 11 风格深色圆角窗口和手绘像素蓝鲸图标。
- 默认 620 × 700，可缩放、置顶、记住位置。
- 呼出后自动聚焦聊天输入框；最大化后自动展开网页边栏。
- 剪贴板快捷问答，支持翻译、润色、总结和解释代码。
- 草稿预览、编辑、复制及填入网页，由用户手动发送。
- 后台启动按需加载网页，可选预加载和低内存目标。

默认呼出快捷键：`Ctrl+Alt+D`，可在设置中改成 `Alt+Space`。剪贴板问答：`Ctrl+Alt+Q`。快捷键可能与其他应用冲突，可使用托盘或标题栏入口。

## 从源码构建

在 Windows PowerShell 或 PowerShell 中进入仓库目录，依次运行：

```powershell
./source/restore.ps1
./source/build.ps1
./DeepSeekMini.exe
```

恢复脚本从 NuGet 下载固定版本 `Microsoft.Web.WebView2 1.0.3537.50`。编译脚本使用 Windows .NET Framework 自带的 C# 编译器，无需额外安装 .NET SDK。运行环境应包含 .NET Framework 4.7.2 或更高。

可选：使用 `source/draw-icon.ps1` 重新生成手绘像素图标。若已安装 Node.js，可运行：

```powershell
node ./source/test-fill-draft.mjs
```

## 数据与隐私

登录状态、缓存及设置保存在程序旁的 `data/` 目录。不要提交、发布或分享该目录。草稿编辑窗口中的内容仅保存在内存中；填入网页后由网页处理。程序不监听剪贴板历史，仅在用户主动操作时读取文字。

源码仓库不包含本机登录数据或编译产物。发布程序时，仅打包程序、依赖、配置、资源和说明，排除 `data/`。

## 已知限制

网页输入框自动聚焦、草稿填入和边栏展开依赖官方页面结构，网站改版后可能需要适配。网页响应速度及运行内存受网页内容、网络和运行环境影响。

详细信息见 [使用说明](使用说明.md) 和 [性能与验证](性能与验证.md)。WebView2 SDK 的第三方许可证见 [WebView2-LICENSE.txt](WebView2-LICENSE.txt)。
