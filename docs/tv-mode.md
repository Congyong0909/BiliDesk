# BiliDesk TV

## 启动

将整个 ZIP 解压到同一个文件夹，双击 `Start-TV.cmd` 进入电视模式，或双击 `BiliDeskTV.exe` 进入桌面模式。在电视版的桌面模式中，打开「应用设置 → 通用 → 电视模式」可在当前屏幕进入电视模式，电视快捷启动会记住最近使用的屏幕。

电视版的配置、缓存和 B 站登录状态位于 `%LOCALAPPDATA%\BiliDeskTV`。首次使用时按窗口内提示操作。原版的安装和配置可以独立使用。

## 遥控器

| 场景 | 方向键 | 确认 | 返回 |
| --- | --- | --- | --- |
| 浏览 | 移动焦点 | 打开卡片或执行按钮 | 返回栏目导航 |
| 播放 | 左右跳转 5 秒，上下调音量 | 打开控制层 | 退出播放 |
| 播放控制层 | 移动焦点 | 执行选中的操作 | 关闭控制层 |
| 画质或选集菜单 | 移动焦点 | 应用选择 | 返回控制层 |

键盘上用 `Enter` 确认，用 `Esc` 或 `Backspace` 返回。搜索时可直接选择热搜和历史词，用 Windows 输入法键入任意关键词。电视设置中可返回桌面模式。

## 更新和构建

源码在 [电视版 Fork](https://github.com/Congyong0909/BiliDesk/tree/feature/tv-mode)，沿用仓库的 GPL-3.0 许可证。使用 Windows 和 .NET 10 x64 SDK 运行以下命令。

```powershell
dotnet build BiliDesk/BiliDesk.csproj -c Release
dotnet test BiliDesk.Tests/BiliDesk.Tests.csproj -c Release
pwsh -File Publish/Publish-Tv.ps1
```

合并上游更新时，从 `feature/tv-mode` 建立临时分支，再合并 `upstream` 的正式发布标签。更新 `AppVersion` 和 `TvVersion` 后，重新构建、测试和打包。通过电视验收后再更新正式分支和标签。升级时退出电视版，将新 ZIP 解压到另一目录后启动。需要回退时重新打开上一版的 `Start-TV.cmd`。如需保留升级前的本地设置，关闭电视版后备份 `%LOCALAPPDATA%\BiliDeskTV`。
