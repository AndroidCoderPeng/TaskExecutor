# TaskExecutor - 定时任务自动化

Windows 桌面端（WPF / .NET 8）的安卓手机定时任务自动化执行器。通过 ADB 无线连接一台 Android
手机，按用户配置的定时计划（支持随机偏移、跳过节假日、每日重置）串行执行"动作链"（亮屏、启动目标
APP、模拟点击、截屏等），执行结果截图归档，并可通过邮件与企业微信机器人发送通知。

## 功能特性

- **无线 ADB 连接**：输入手机 IP 即可 `adb connect ip:5555` 无线连接，内置心跳保活（掉线自动重连自愈，连续 2 次失败才判定掉线，抗
  Wi-Fi 抖动）
- **任务调度**：30
  秒一个调度周期，按计划时间串行执行任务动作链；支持随机时间偏移（避免定点打卡过于规律）、每日重置、跳过节假日（法定节假日 +
  自定义工作日 + 调休三层规则）
- **动作链**：亮屏、启动指定 APP（预置钉钉/企业微信/飞书）、模拟点击/滑动、等待、截屏等，支持拖拽排序
- **截图归档**：截屏产生的 PNG 按天分组展示，统计总数/大小，支持放大查看与清理
- **通知推送**：SMTP 邮件（MailKit）+ 企业微信群机器人 Webhook，可按任务选择通知方式
- **数据导入导出**：配置一键备份/恢复（AES 加密）
- **指令测试**：手动发送 ADB 指令（亮屏、tap/swipe 等）验证设备权限
- **内置 FAQ**：WebView2 渲染本地常见问题文档（含小米 MIUI/HyperOS ADB 模拟点击权限设置、邮箱授权码、企微 Webhook 限流等）

## 页面一览

| 分组 | 页面                | 功能                                                                              |
|------|---------------------|-----------------------------------------------------------------------------------|
| 管理 | 无线连接            | ADB 无线连接设备，显示连接状态/机型/心跳                                          |
| 管理 | 任务管理            | 任务列表：新建/编辑（计划时间、动作链、通知方式）、启停开关、拖拽排序、右键试运行 |
| 管理 | 记录归档            | 截图按天归档展示与管理                                                            |
| 管理 | 目标 APP            | 维护目标安卓应用清单（名称 + PackageName + Activity）                             |
| 配置 | 数据导入导出        | `app-config.json` 各配置段备份/恢复                                               |
| 配置 | 邮箱配置            | SMTP 服务器/端口/发件人/授权码/收件人                                             |
| 配置 | 企业微信配置        | 群机器人 Webhook Key、消息标题、是否附带截图                                      |
| 配置 | 任务等待时间        | 动作链中"等待"步骤的全局时长                                                      |
| 配置 | 任务重置时间        | 每日任务计划的重置时间点                                                          |
| 配置 | 随机时间范围        | 计划触发时间的随机偏移上下限                                                      |
| 调度 | 调度开关            | 全局调度开关、跳过节假日、每日重置、自定义工作日                                  |
| 调试 | 指令测试            | 手动向设备发送 ADB 指令                                                           |
| 帮助 | 常见问题 / 关于应用 | 内置 FAQ 文档、版本信息                                                           |

## 技术栈

- **框架**：.NET 8（net8.0-windows）、WPF、MVVM（CommunityToolkit.Mvvm 8.4.2）
- **主要依赖**：
    - `gong-wpf-dragdrop` — 任务/动作链拖拽排序
    - `MailKit` — SMTP 邮件通知
    - `Microsoft.Web.WebView2` — FAQ 页面渲染
    - `Newtonsoft.Json` — 配置/计划/日志序列化
    - `Microsoft.Xaml.Behaviors.Wpf` — XAML 事件绑定命令
    - `Microsoft.Extensions.DependencyInjection` — DI 容器（Services 均为单例）
- **设备通信**：外部 `adb.exe` 子进程（随程序发布 Android platform-tools 工具链，位于 `ADB\` 目录）

## 目录结构

```
TaskExecutor/
├── ADB/                  # Android platform-tools（adb.exe 等），发布时自动拷贝
├── BuildScript/          # 构建打包脚本（见下文）
├── TaskExecutor/         # WPF 主项目
│   ├── Views/            # 页面（14 个功能页）
│   ├── ViewModels/       # MVVM 视图模型
│   ├── Services/         # 调度器、ADB 心跳、日志、Toast、对话框等服务
│   ├── Model/            # 任务、动作链等数据模型
│   ├── Cache/            # 配置缓存与 section 定义
│   ├── Utils/            # ADB 封装、邮件/企微发送、节假日判定、执行计划等
│   ├── Controls/         # 自定义控件（TimePicker 等）
│   ├── Dialogs/          # 任务编辑对话框
│   ├── Faq/              # 内置 FAQ（faq.html/css，WebView2 加载）
│   ├── Fonts/ Images/    # 字体图标与图片资源
│   └── Styles/ Colors/   # 统一主题样式
```

## 数据存储

所有配置与运行数据（除截图外）保存在 `%AppData%\TaskExecutor\`：

| 文件/目录                 | 内容                                                                                   |
|---------------------------|----------------------------------------------------------------------------------------|
| `app-config.json`         | 主配置（设备 IP、任务、目标 APP、邮箱、企微、调度参数等），写入采用临时文件 + 原子替换 |
| `execution-plan.json`     | 执行计划（计划时间、随机偏移、状态），重启后保持                                       |
| `logs\*.jsonl`            | 运行日志，按天写入，保留 7 天                                                          |
| `<安装目录>\Screenshots\` | 任务截屏 PNG 归档                                                                      |

## 构建与发布

环境要求：.NET 8 SDK、Inno Setup 6。

1. 按需调整 `BuildScript\build_release.bat` 顶部参数（配置、RID、是否自包含、Inno Setup 路径、输出目录）
2. 运行脚本，流程为：
    - `dotnet publish -c Release -r win-x64 --self-contained true` 发布到 `publish\`（含 .NET 运行时、ADB、FAQ）
    - Inno Setup 编译 `TaskExecutor.iss` 生成安装包
3. 最终产物：`TaskExecutor_Setup.exe`（默认输出到桌面，自包含安装，目标机无需安装 .NET 运行时）

```bat
cd BuildScript
build_release.bat        # 打包后清理 publish 目录
build_release.bat keep   # 打包后保留 publish 目录
```

## 使用须知

- 手机需开启 USB 调试并与电脑处于同一局域网；小米 MIUI/HyperOS 还需开启「USB 调试（安全设置）」才能模拟点击
- 邮箱通知需使用 SMTP 授权码（如 QQ 邮箱 535 授权码），非登录密码
- 首次运行后从「无线连接」页连接设备，再进入「任务管理」创建任务；「任务管理」「指令测试」需设备连接后才可用
