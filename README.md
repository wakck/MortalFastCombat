# MortalFastCombat — 《活侠传》战斗加速与重复敌人跳过

当前版本 **1.1.3，Windows x64 / Unity Mono / BepInEx 6**。
适配 Steam Build **26237817**、Unity **2020.3.49f1**。

**[下载 v1.1.3 共存安装包及 SHA-256 校验文件](https://github.com/wakck/MortalFastCombat/releases/tag/v1.1.3)**

新增跳过战斗快捷键：**键盘 F10、手柄 R3（按下右摇杆）**，可独立禁用或改键。只有原有跳过按钮可用时才生效，长按不会重复结算；见 [快捷键说明](docs/HOTKEYS.zh-CN.md)。

本版安装包需要已安装指定的 **LOMAssistant 3.3.2 + BepInEx 6.0.0-be.789**，不包含助手或加载器。
旧版 v1.1.0 使用 BepInEx 5，两套加载器不能直接叠加。迁移步骤见 [共存配置](docs/LOMASSISTANT.zh-CN.md)。

## 安装与更新

1. 退出游戏，先按 [LOMAssistant 上游发布页](https://github.com/kevinnie2003/LOMAssistant/releases) 安装已验证的 3.3.2 配套运行环境；版本号及 DLL 指纹均需匹配。
2. 将本安装包内整个 `MortalFastCombat` 文件夹解压到游戏目录，与 `Mortal.exe` 同级。
3. 在游戏目录打开 PowerShell，首次安装执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\MortalFastCombat\Install.ps1
```

已有加速插件时执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\MortalFastCombat\Update.ps1
```

首次安装把助手全局设为 **2 倍**，新用户战斗默认 **4 倍**；更新保留已有全局值、战斗倍率、配置和胜利记录。
可用 `-GlobalSpeed 2` 明确指定全局值。脚本仍会关闭助手的流程压缩和单挑直接获胜，备份旧插件及相关配置，启用 `HideManagerGameObject=true`；不替换加载器或游戏 DLL。

正常从 Steam 启动游戏即可。校验不匹配会停止安装，不会自动下载最新版助手或修改存档。

## LOMAssistant 共存配置

`BepInEx/config/LOMAssistant.cfg` 中：

```ini
[LOMAssistant]
GameSpeed = 2
EnableFlowSpeedup = false
EnableCombatAssist = false
```

- **剧情、探索**：助手的全局倍速生效，可在其面板调整。
- **回合制战斗**：助手暂时让出倍率，使用本模组的 **2／4／8 倍**，均相对于游戏原速，不再叠乘。
- **胜败结算至场景退出**：保持倍率交接；返回剧情后由助手应用当前全局值。战中改全局值会在战后生效。
- **暂停**：改变倍率不会解除暂停；助手原有的即时群战、自动对话免全局加速规则继续有效。

助手可能仍按方法名显示 `ShowAction`、`NextInputAction` 重叠提示；对应输入保护和敌方行动预告，测试期间保留，详见共存文档。

## 战斗功能

- 沿用游戏“快速战斗”按钮，战斗设置可选 2／4／8 倍。
- 合并已审查 Buff 的空帧等待，共享每帧 256 次推进／约 2 毫秒预算；保留触发顺序、连锁增删与清理。
- 精简血条、内力条、数字提示和操作面板过渡，保留角色出招、玩家操作及必要对话。
- 同周目真实击败过的相同普通敌人，再次遇到时可在首条指令前手动“跳过并获胜”。
- 严格匹配敌人内部标识、属性、技能参数、天赋、初始 Buff、行动与难度规则；玩家变弱仍可跳过。
- 调用本场原生胜利结算，不重放旧奖励；剧情事件、条件结算、条件对话或无法完整识别的战斗拒绝跳过。

关闭快速战斗后，回合制战斗为 1 倍，停止 Buff 压缩与提示精简；跳过受模组总开关及独立选项控制。
关闭模组总开关后保留原生快速战斗倍率；助手在 Combat 场景仍让出倍率，避免叠乘。

## 存档、配置与隐私

仓库及本版安装包不含个人存档、胜利记录、个人配置、备份、测试运行数据、日志或游戏程序集。
仓库仅保留模组源码、构建与安装维护文件及使用说明。

配置由游戏首次加载插件后生成于 `BepInEx/config/local.legendofmortal.fastcombat.cfg`。
胜利记录独立位于 `MortalFastCombat/data/<用户>/history-*.json`，不改原存档格式。
仅记录安装后真实完成结算的胜利；跳过、失败及中断不新增记录。
新游戏建立新周目，未知旧存档默认独立，可在战斗设置中手动关联。

## 卸载与回退

```powershell
.\MortalFastCombat\Uninstall.ps1 -WhatIf
.\MortalFastCombat\Uninstall.ps1
# 需要同时移除本插件配置时，额外加 -RemoveConfiguration。
```

卸载仅移除清单内、指纹匹配的本插件，默认保留配置、胜利记录、助手和共享加载器。
卸载后战斗速度由游戏及助手处理。独立记录仅在明确运行 `Clear-History.ps1` 时清理，不会自动删除。

`Rollback.ps1` 可恢复最近一次安装前的插件、助手配置及加载器配置，不操作存档和胜利记录。
若安装后配置已有变化，回退脚本会停止以保留后续设置，可从 `MortalFastCombat/backup/` 手动恢复所需文件。

## 从源码构建

需要 Windows .NET Framework 4 C# 编译器，以及本机兼容游戏和 BepInEx 6 依赖。

```powershell
.\Build.ps1 -GameDirectory '你的游戏目录'
```

产物为 `dist/MortalFastCombat.dll`，构建不会安装。游戏引用、助手及加载器均由本机读取，不进入仓库。
本版不使用旧版的 BepInEx 5 依赖提取脚本。游戏和助手更新均需重新审查指纹。

手柄快捷键通过虚拟输入验证，实体设备及自定义 Steam Input 映射需在本机确认。

## 第三方组件

非官方模组，与游戏开发商及 Steam 无隶属关系。
本版安装包仅包含本项目插件及维护文件，助手和 BepInEx 由用户另行安装。
第三方说明见 [THIRD_PARTY.md](THIRD_PARTY.md)。本项目尚未单独指定源码许可证，第三方许可仅适用于对应组件。
