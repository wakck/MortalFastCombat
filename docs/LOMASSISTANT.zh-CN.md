# LOMAssistant 3.3.2 共存与迁移

## 已验证组合

| 项目 | 要求 |
|---|---|
| 游戏 | Steam Build 26237817，Unity 2020.3.49f1，Windows x64 Mono |
| 加速插件 | MortalFastCombat 1.1.3，原生 BepInEx 6 |
| 助手 | LOMAssistant 3.3.2 |
| 加载器 | 助手配套的 BepInEx 6.0.0-be.789 |

助手 DLL SHA-256：

```text
8504ADE709EE4343F0C2FDD134B12A0E2CC6D62D642A4E60E6306CD97A910660
```

同版本号也需核对指纹。安装脚本检查游戏、助手和关键加载器 DLL；运行时再次检查游戏和助手指纹及所需私有字段。
其他助手版本未被视为兼容。上游后续更新可能需要重新适配，不应盲目覆盖。

## 配置

```ini
[LOMAssistant]
GameSpeed = 2
EnableFlowSpeedup = false
EnableCombatAssist = false
```

`GameSpeed` 是助手全局倍速，可在其面板改变；`EnableFlowSpeedup` 是流程压缩，
`EnableCombatAssist` 是单挑直接获胜，后二者关闭，由本插件负责战斗压缩及严格匹配的跳过。
其他助手设置保留。安装脚本备份配置后仅编辑这三项，以及 BepInEx 的 `[Chainloader] HideManagerGameObject=true`。

全局 2 倍与战斗 8 倍不会成为 16 倍。整个 Combat 场景暂时停止助手的倍率叠乘，
包含 `GameOver` 至场景实际退出之间的结算；离开后恢复未乘全局值的基础速度，
让助手原方法应用当前全局设置。暂停保持 0，助手对 Battle 和自动对话的免加速逻辑继续有效。

## 从 v1.1.0 / BepInEx 5 迁移

1. 退出游戏，将旧 `BepInEx` 整目录移到自己保留的备份目录，并备份游戏根目录的
   `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`changelog.txt`。
   不要移动或删除正式 AppData 存档、`MortalFastCombat/data` 胜利记录。
2. 安装指定 LOMAssistant 3.3.2 配套 BepInEx 6。不要将 BepInEx 5 和 6 的 core 文件混放。
   上游安装器可能联网获取最新版或移动其他模组，本项目的安装脚本不会运行它。
3. 解压本共存包；已有加速插件时运行 `Update.ps1`，新安装运行 `Install.ps1`。
   若手动环境中现有插件指纹未知，脚本会停止，不会强行覆盖。
4. 按需从备份恢复自己的加速配置文件。此文件和独立胜利记录格式保持兼容；不要恢复旧的加载器文件。

从 BepInEx 6 的本地 1.1.1 / 1.1.2 / 1.1.3 更新，无需替换加载器；运行 `Update.ps1` 即可。
更新默认保留助手现有全局值；希望开启 2 倍时用 `Update.ps1 -GlobalSpeed 2`，或在助手面板调整。

本版脚本只拥有自己的插件，新的卸载清单不认领助手或加载器。旧清单完整保存在本地备份，回退可恢复。

## 关于助手的“冲突 Mod”提示

助手按 Harmony 补丁的方法名交集提示 `CombatUIManager.ShowAction` 和 `CombatManager.NextInputAction`。
前者用于助手敌方行动预告和本插件首轮准备标记，后者用于面板输入保护。
共存测试期间这些功能均保留，状态、随机数和回调对照通过；未屏蔽通用提示。
这不是对未来版本或其他模组组合的兼容保证。

## 安装包内容与回退

共存 ZIP 包含插件、安装/更新/卸载/回退脚本、通用兼容性清单、中文说明及包内文件哈希。
不附助手、加载器、游戏文件、任何人的存档/胜利记录/配置、测试 DLL、测试数据或日志。

安装前的插件与相关配置保存在 `MortalFastCombat/backup/install-1.1.3-*`。
退出游戏后运行 `Rollback.ps1` 回退。若配置后来变化，脚本会停止；手动恢复时请只恢复自己需要的插件或配置，避免覆盖后续设置。
不需要回滚存档或删除胜利记录。
