# 测试源码与隔离要求

`../Test.ps1` 执行 20 项协程调度、13 项记录持久化和 16 项快捷键状态机断言，不启动游戏。
`PackageTests.ps1` 在独立模拟目录检查公开安装脚本，读取本机依赖构造模拟环境；不运行游戏、不操作正式存档。

```powershell
.\Test.ps1
.\tests\PackageTests.ps1 -GameDirectory '本机兼容游戏目录'
```

## 游戏内集成工具

`integration/` 包含 1.1.3 快捷键与既有功能实测工具的源码，没有存档、记录、日志、CSV 或构建产物。
这些文件不属于运行安装包，不应安装到日常游玩的环境。

使用时必须先建立完整、无目录链接的独立游戏副本，准备自己的备份存档，再核查 `IsolationPatcher.cs` 的重定向实现。
所需布局如下，所有路径都在独立副本内：

- `isolation-root.txt` 内容为 `COEXISTENCE_TEST_ONLY`；这只是测试标记，不应放入正式游戏。
- `sandbox/persistent/coexist-test/`：测试存档副本。
- `validation/battle-original.dat`：自己的原始战斗测试夹具，仓库不提供。
- `validation/test-mode.flag`：显式启用测试主机。

运行 `Build-Validation.ps1 -GameDirectory '独立游戏副本'`，在 `tests/artifacts` 生成两个测试 DLL。
将 **IsolationPatcher.dll** 装到独立副本 `BepInEx/patchers`，**ValidationPlugin.dll** 装到 `BepInEx/plugins`。
缺失启动前重定向、就绪日志或标记时，不得继续。测试主机在 Awake 中核验 persistentDataPath，否则终止进程。
游戏参数 `-logFile validation/player.log` 单独重定向日志，测试预加载器禁止 PlayerPrefs 写入。

等待 `HANDOFF STARTUP FIXTURES READY` 后使用 `Send-TestCommand.ps1 -GameDirectory ... -Command ...`。
可用 `hotkey-tests`、`handoff-tests`、`v110-tests`、`global2`、`global4`、`speed4`、`speed8`、`late`、`ordinary`、`replay-fast`、
`fresh-history`、`persistence-check`、`coexist`、`quit`。前一长测试未完成时不要启动下一项。

若命令、存档或场景缺少适配条件，先停止测试并检查日志。退出进程后再移除测试 DLL。
当前工具用于已验证布局，不是对任意副本自动保证隔离的通用启动器。
隐藏窗口的快捷键测试还需副本根目录的 `hotkey-virtual-input.flag`；仅在隔离副本中将焦点查询强制为 true，并通过虚拟 Keyboard / Gamepad 发送事件。实体手柄与 Steam Input 映射需要另行确认。
历史汇总在 `../docs/VALIDATION.zh-CN.md`，本版见 `../docs/VALIDATION-1.1.3.zh-CN.md`。
