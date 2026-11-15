1、实际编译时如需调整，改 build_release.bat 顶部参数：CONFIG / RID / SELF_CONTAINED / INNO_SETUP / OUTPUT_DIR
2、电脑需要安装 .NET 8 SDK 和 Inno Setup Compiler
3、直接双击或在 CMD 中运行 build_release.bat，流程：
   ① dotnet publish 发布到 <项目根>\publish（自包含模式，含 .NET 运行时、ADB、Faq）
   ② 调用 TaskExecutor.iss 打包，在 OUTPUT_DIR（默认桌面）生成 TaskExecutor_Setup.exe
   ③ 默认删除 publish 目录；如需保留，运行：build_release.bat keep
4、升级版本号：同时修改 build_release.bat 无关，改 TaskExecutor.iss 里的 #define MyAppVersion
