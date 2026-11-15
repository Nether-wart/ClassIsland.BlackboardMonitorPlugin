# 黑板开合监视

用摄像头判断移动黑板的开合状态，并把状态变化交给 ClassIsland 的自动化触发器，由用户自己决定要做什么
（例如把主界面移动到没有被遮挡的屏幕上）。

## 能做什么

插件只区分四种状态：

| 状态 | 含义 |
| --- | --- |
| 全部打开 | 两侧都没有黑板进入摄像头画面 |
| 左侧合上 | 画面左半边被黑板遮挡 |
| 右侧合上 | 画面右半边被黑板遮挡 |
| 全部合上 | 整个画面（检测区域）都被遮挡 |

> “左/右”指**摄像头画面**的左右，不是站在讲台上的左右。请先打开插件设置页看实时预览，确认你那边
> 的“左侧合上”对应哪一块黑板。

插件提供：

- 一个设置页（设置 → 扩展 → 黑板开合监视），包含实时预览、检测区域和阈值调整；
- 一个自动化触发器「黑板开合状态变化时」，可以指定“变为某个状态时触发”，也可以选择离开该状态时触发
  工作流的恢复。

插件本身**不会**主动移动窗口，具体行为全部由工作流决定。

## 环境要求

- ClassIsland 2.0+；
- Windows（当前只打包了 Windows 的 OpenCV 原生库）；
- 一个能被 ClassIsland 访问的摄像头。Windows 11 需在「设置 → 隐私和安全性 → 摄像头」中允许桌面应用
  访问摄像头。

## 安装

1. 构建或获取 `ClassIsland.BlackboardMonitorPlugin.cipx`；
2. 在 ClassIsland 的「设置 → 插件」中安装并启用插件，然后重启应用。

插件包约 60MB，其中绝大部分是 OpenCV 的原生库（同时包含 x86 与 x64，以便在两种架构的 ClassIsland 上
都能运行）。

## 配置

打开「设置 → 扩展 → 黑板开合监视」：

1. **摄像头**：先填摄像头序号（通常内置摄像头是 0，外接摄像头依次是 1、2……）。设置页一打开就会尝试
   连接，画面出现在「实时预览」里。如果一直连不上，点「重新连接摄像头」；如果仍失败，检查摄像头是否被
   其它程序占用。
2. **检测区域**：黄框表示只在这个矩形内统计。默认裁掉画面下方 35%，可以避开课桌、学生头发等深色物体
   造成的干扰。**这是最值得先调的一项**——把框收紧到黑板实际会出现的区域，误判会明显减少。
3. **判定阈值**：一般不用动。全部打开时被误判成有遮挡，就把「黑色阈值」调小或把「列黑像素占比」调大；
   合上时识别不到，就反过来调。

设置会自动保存到 `Config/Plugins/classisland.blackboard-monitor/Settings.json`。

预览画面里的标注：

- **黄框**：检测区域；
- **白线**：检测区域的中线，用于判断遮挡区域偏左还是偏右；
- **红框**：判定出来的最长连续遮挡区域；
- 左上角文字：这一帧的判定结果。

正常情况下，红色区域偏左 → 左侧合上，偏右 → 右侧合上，铺满整个检测区域 → 全部合上，没有红框 →
全部打开。

## 在自动化里使用

新建一个工作流，添加触发器「黑板开合状态变化时」：

- **当黑板状态变为**：四选一；
- **离开该状态时触发工作流的恢复**：勾选后，状态离开目标值时调用 `TriggerRevert()`，配合可恢复的行动
  可以回到原来的状态；
- **插件启动时若已处于该状态，也触发一次**：应用启动时如果黑板已经是合上的，也执行一次工作流。

### 示例：把主界面移动到没被遮挡的屏幕

假设两块屏：主屏在左，副屏在右；摄像头看到的黑板合上时，对应的屏幕会被挡住。

1. 触发器 A：目标状态 = `右侧合上` → 行动「应用设置」，把 `WindowDockingMonitorIndex` 设为 `0`
   （主界面移到 1 号屏）。
2. 触发器 B：目标状态 = `全部打开`（或 `左侧合上`）→ 行动「应用设置」，把 `WindowDockingMonitorIndex`
   设回 `1`。

`WindowDockingMonitorIndex` 走的是 ClassIsland 自己的窗口停靠逻辑，主界面会在下一次计时器 tick 时自动
移动到目标屏幕，不需要重启。

> 提示：`WindowDockingMonitorIndex` 从 0 开始，对应「设置 → 窗口 → 窗口停靠屏幕」里的顺序，改完后可以
> 在设置页确认。

## 检测原理

1. 摄像头画面缩到 160×90 灰度图（只为统计，不追求清晰度）；
2. 在检测区域内逐列统计“灰度低于阈值的像素”占比，占比超过阈值的列记为被遮挡；
3. 找出最长的连续被遮挡列区间，得到它的**宽度**、**中心位置**，以及被遮挡列占全部列的比例；
4. 判定：
   - 区间宽度太小 → 全部打开；
   - 区间足够宽且中心接近检测区域中点（或遮挡列总占比极高）→ 全部合上；
   - 否则按区间中心落在中线的哪一侧，判定为左侧合上或右侧合上；
5. 连续 N 帧结果一致才认为状态真的改变（默认 3 帧），避免抖动。

之所以先求“连续遮挡区间”再判断位置，而不是直接比较左右两半的黑像素比例：合上的黑板经常会越过画面中线，
直接把画面切两半统计会让另一半的数据被污染。用样例视频验证时，某一帧左半边已经合上，右半边的黑像素占比
仍有 0.39，只看半区比例会误判成两侧都合上；而最长遮挡区间的中心（0.32）能明确指回“左侧合上”。

样例视频（1920×1080，26 秒）抽帧验证结果：全部打开时单列最高黑像素占比不超过 0.41，有遮挡时不低于 0.78，
默认阈值 0.5 有足够余量。

## 已知限制

- **只有 Windows**：插件包只附带 Windows 的 OpenCV 原生库。若需要在 Linux/macOS 上使用，需要在
  对应平台上构建，并把 `OpenCvSharp4.runtime.*` 换成对应平台的包，同时修改 `manifest.yml` 的
  `supportedOSPlatforms`。
- **需要摄像头视角稳定**：摄像头被人碰歪后需要重新调整检测区域。
- **强光/逆光**：如果画面整体过亮，黑白对比不足时可能漏检，可适当调高「黑色阈值」。
- **只支持四种状态**：不支持“半开一半”之类的中间态；黑板在移动过程中的过渡帧可能被识别成其中一侧合上，
  防抖帧数可以减轻这个影响。
- **摄像头按需占用**：只有存在已加载的触发器或设置页预览时才会打开摄像头；没有配置触发器时不会占用。

## 构建

```bash
dotnet build ClassIsland.BlackboardMonitorPlugin/ClassIsland.BlackboardMonitorPlugin.csproj -c Debug
```

产物：

- `ClassIsland.BlackboardMonitorPlugin/bin/Debug/net8.0/`：插件文件（可用 `--externalPluginPath`
  直接加载调试）；
- `ClassIsland.BlackboardMonitorPlugin/cipx/ClassIsland.BlackboardMonitorPlugin.cipx`：可安装的插件包。

## 目录结构

```
ClassIsland.BlackboardMonitorPlugin/
├── Plugin.cs                        插件入口，注册设置页与触发器
├── manifest.yml                     插件清单
├── Automation/
│   ├── BlackboardStateTrigger.cs         触发器
│   └── BlackboardStateTriggerSettings.cs 触发器设置
├── Models/
│   ├── BlackboardState.cs                状态枚举与显示名
│   ├── BlackboardDetectionResult.cs      检测参数与结果
│   ├── BlackboardMonitorSettings.cs      插件设置
│   └── CameraBackend.cs                  取流后端
├── Services/
│   ├── CameraCapture.cs                  OpenCV 取流封装
│   ├── BlackboardDetector.cs             检测算法（纯计算，不依赖 OpenCV）
│   ├── BlackboardMonitorService.cs       后台采集循环、防抖、预览
│   ├── BlackboardMonitorEvents.cs        事件参数
│   └── BlackboardMonitorSettingsStore.cs 设置读写
└── Views/
    ├── SettingsPages/BlackboardMonitorSettingsPage.axaml(.cs)          设置页
    └── Automation/BlackboardStateTriggerSettingsControl.axaml(.cs)     触发器设置
```
