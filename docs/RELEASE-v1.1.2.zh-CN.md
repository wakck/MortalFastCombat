# v1.1.2 — LOMAssistant 共存与全局倍速交接

本版原生使用 **BepInEx 6**，需要已安装经指纹核对的 **LOMAssistant 3.3.2 + BepInEx 6.0.0-be.789**。
适配 Steam Build **26237817**、Unity **2020.3.49f1**、Windows x64 Mono。

## 下载

- `MortalFastCombat-1.1.2-Windows-x64-LOMAssistant.zip`：共存安装包，仅包含我们的插件、维护脚本及说明；不附助手或加载器。
- 同名 `.zip.sha256`：安装包 SHA-256 校验文件。
- 仓库源码、BepInEx 6 构建脚本和中文说明同步更新。

## 倍率与功能

- 剧情、探索使用助手全局倍速，首次安装默认 **2 倍**，可在助手面板调整。
- 回合制战斗使用我们的 **2／4／8 倍**，新用户默认 4 倍；与助手全局倍率不叠乘。
- 胜败结算期间维持交接，离开战斗后恢复助手当前全局设置，暂停仍有效。
- 保留 Buff 空等待压缩、精简战斗提示，以及同周目严格匹配敌人的手动跳过。
- 助手共存配置：`GameSpeed=2`（可调整）、`EnableFlowSpeedup=false`、`EnableCombatAssist=false`。
  助手其他设置及其原有免加速规则保留。

## 安装与更新

1. 退出游戏，先安装文档所列的助手及 BepInEx 6 组合。
2. 将 ZIP 中的 `MortalFastCombat` 文件夹解压到游戏目录，与 `Mortal.exe` 同级。
3. 首次运行 `Install.ps1`；已有本插件则运行 `Update.ps1`。更新保留已有全局值、战斗配置和胜利记录。
   若希望明确设为全局 2 倍，运行 `Update.ps1 -GlobalSpeed 2`。
4. 正常从 Steam 启动游戏。

**旧 v1.1.0 使用 BepInEx 5，不能直接把两套加载器混放。**
迁移及助手指纹说明见 [共存文档](https://github.com/wakck/MortalFastCombat/blob/v1.1.2/docs/LOMASSISTANT.zh-CN.md)。
安装脚本核对版本并备份插件/相关配置；本包不替换游戏 DLL、助手 DLL或加载器，不读取或改写存档和胜利记录。
卸载保留助手、加载器和记录；包内 `Rollback.ps1` 提供配置完整性保护的回退。

## 验证

30 项倍率交接、33 项既有功能检查通过；三组整场对照共 **283 个状态检查点、3,139 次 Buff 回调逐字节一致**，
**5,482 次倍率采样无叠乘**，重启后的胜利记录和跳过通过。公开安装包另通过 32 项模拟安装/更新/回退/卸载检查。
探索与群战分支使用场景探针，未验证完整探索或群战流程；未穷举助手非战斗功能。
详细范围见 [验证汇总](https://github.com/wakck/MortalFastCombat/blob/v1.1.2/docs/VALIDATION-1.1.2.zh-CN.md)。

**仓库和附件不含个人存档、胜利记录、个人配置、备份、测试运行数据、日志或游戏程序集。**
测试源码和汇总结论保留供审查，不提供测试夹具或输出文件。

安装包 SHA-256：

```text
DC3FD3C235FF26109CAEE62860414C209D8AAD3220C73CD2E637D056B4FB23DF
```
