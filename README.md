# MortalFastCombat — 《活侠传》战斗加速与重复敌人跳过

Windows x64 / Unity Mono / BepInEx 5.4.23.6，插件版本 **1.1.0**。
适配 **Steam Build 26237817、Unity 2020.3.49f1**。仅作用于回合制单挑。

## 下载与安装

**[下载纯运行安装包](https://github.com/wakck/MortalFastCombat/raw/refs/heads/main/release/MortalFastCombat-1.1.0-Windows-x64-Runtime.zip)** · [SHA-256](release/MortalFastCombat-1.1.0-Windows-x64-Runtime.zip.sha256)

1. 退出游戏，将安装包中的整个 `MortalFastCombat` 文件夹解压到游戏目录，与 `Mortal.exe` 同级。
2. 在游戏目录打开 PowerShell，运行 `./MortalFastCombat/Install.ps1`。
3. 正常从 Steam 启动游戏。默认 4 倍、Buff 空等待压缩和精简战斗提示开启。

已经通过本模组脚本安装并保留安装清单时，用包内 `Update.ps1` 更新；保留自己的配置和记录。
已有其他 BepInEx 5 模组时，完整安装脚本会拒绝覆盖。可仅放入
`dist/MortalFastCombat.dll` 至 `BepInEx/plugins/MortalFastCombat/`，并确保
`BepInEx/config/BepInEx.cfg` 的 `[Chainloader]` 下为 `HideManagerGameObject = true`。
手动安装不生成本包的卸载清单，不要用本包卸载脚本删除其他模组的加载器。

## 功能与使用

- 沿用游戏原“快速战斗”按钮，战斗设置提供相对于原速的 **2／4／8 倍**。
- 合并已审查 Buff 协程的空等待，保留属性修改、触发顺序、连锁增删和清理。
- 精简血条、内力条、数字提示与指令面板过渡，保留角色出招、输入与必要对话。
- 同周目真实击败过的相同普通敌人，再次遇到时在首条指令前提供手动“跳过并获胜”。
- 敌人的内部标识、全部战斗属性、技能参数、天赋等级、初始 Buff 与行动／难度规则严格匹配；不要求玩家与上次一样强。
- 跳过调用当前战斗的原生胜利结算，不模拟回合，不补扣战斗消耗，不重放旧奖励。
- 中途剧情事件、条件结算、条件对话以及无法完整识别的配置不提供跳过。

关闭原快速战斗按钮会停止加速、Buff 压缩与提示精简；跳过按钮受模组总开关及独立选项控制。
暂停时不推进压缩，退出战斗清理倍率。剧情、养成和即时制群战保持原行为。

## 存档与记录

仓库和运行安装包均不包含个人存档、胜利记录、配置、备份或测试运行数据，也不包含游戏原始 DLL。
安装后才开始记录胜利，不推测安装前打败过哪些敌人。
记录独立写入 `MortalFastCombat/data/<用户>/history-*.json`，原存档格式不变。
新游戏创建独立周目；同周目回读旧档可复用胜利记录。未知旧存档默认独立，
可在战斗设置中明确关联至同一周目。跳过、失败与退出不会新增胜利。

配置生成于 `BepInEx/config/local.legendofmortal.fastcombat.cfg`。
日志生成于 `BepInEx/LogOutput.log`。

## 卸载

退出游戏后，在游戏目录执行：

```powershell
./MortalFastCombat/Uninstall.ps1 -WhatIf
./MortalFastCombat/Uninstall.ps1
```

卸载按安装清单核对哈希，保留胜利记录和其他插件使用的共享加载器。
仓库内的 `Clear-History.ps1` 可单独预览／清理模组记录；先用 `-WhatIf` 检查。

## 从源码构建

需要 Windows 自带 .NET Framework 4 C# 编译器和本机安装的兼容游戏，不需要 .NET SDK。
BepInEx 构建依赖从本仓库已校验的干净运行包提取；游戏引用仅从本机读取，不会复制进仓库。

```powershell
./Build.ps1 -GameDirectory '你的游戏安装目录'
./Test.ps1
```

产物位于 `dist/MortalFastCombat.dll`。构建保留已经审查的程序集指纹和 Buff 白名单，
不自动适配游戏更新。运行时指纹不匹配时停用增强并保留游戏原行为。
测试工具说明见 [tests/README.md](tests/README.md)。

## 验证与限制

相较 1.0.1，4／8 倍实测整回合额外缩短约 **6–7%**，血条等待降至 0 帧；
后期战斗每组 **109 个状态检查点、1,387 次 Buff 移除通知**逐项一致。
调度器与记录模块共 33 项自动断言，另有 33 项游戏内检查及进程重启持久化验证。
完整结论和测试边界见 [验证报告](docs/VALIDATION.zh-CN.md)。

游戏的表现抖动会逐帧使用全局随机数；不同速度下普通游玩不保证重现同一战果。
正式插件不修改随机系统。协程压缩共享每帧 256 次／约 2 毫秒预算，未知等待仍交由 Unity。
新版本游戏需要重新审查适配。

## 第三方组件

本项目为非官方模组，与游戏开发商及 Steam 无隶属关系。
运行包内第三方组件的来源和许可见 [THIRD_PARTY.md](THIRD_PARTY.md) 与 [licenses/](licenses/)。
本仓库未额外指定项目源码许可证；第三方许可只适用于对应组件。
