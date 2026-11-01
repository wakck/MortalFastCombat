# v1.1.3 — 键盘与手柄跳过快捷键

新增 **F10**（键盘）和 **R3 / 按下右摇杆**（手柄）跳过战斗。按键会显示在“跳过并获胜”按钮上，可在配置文件 `[Hotkeys]` 内改键或分别关闭。

- 仅原跳过按钮可用时生效：同周目真实击败过的相同普通敌人、首条指令前；不放宽敌人匹配规则。
- 长按不会重复结算；暂停、设置面板、助手弹窗和文字输入期间拦截快捷键。恢复后先松开再按。
- 跳过仍调用本场原生胜利流程，奖励只执行一次。R3 在不能跳过时保留原游戏选择功能。
- 助手默认 L3 打开面板，可与默认 R3 共存；若助手 `GamepadPanelButton=1`，请改动其中一方。详见 [快捷键配置](https://github.com/wakck/MortalFastCombat/blob/v1.1.3/docs/HOTKEYS.zh-CN.md)。

## 下载与安装

下载 `MortalFastCombat-1.1.3-Windows-x64-LOMAssistant.zip` 及同名 `.zip.sha256`。
需要已安装经指纹验证的 **LOMAssistant 3.3.2 + BepInEx 6.0.0-be.789**；适配 **Steam Build 26237817 / Unity 2020.3.49f1 / Windows x64 Mono**。本包不包含助手、加载器或游戏程序集。

退出游戏，将 ZIP 内 `MortalFastCombat` 文件夹解压到游戏目录，与 `Mortal.exe` 同级。首次执行 `Install.ps1`；已有插件执行 `Update.ps1`，然后正常启动游戏。更新保留倍率、配置及胜利记录，新增快捷键默认自动生效；脚本保留旧插件及相关配置供回退。旧 BepInEx 5 用户需先按 [迁移文档](https://github.com/wakck/MortalFastCombat/blob/v1.1.3/docs/LOMASSISTANT.zh-CN.md) 迁移。

助手共存配置继续为 `GameSpeed=2`（可调整）、`EnableFlowSpeedup=false`、`EnableCombatAssist=false`。剧情探索由助手全局倍速控制，回合制战斗由本模组 2/4/8 倍控制，战后恢复全局值。

## 验证与隐私

47 项快捷键集成、33 项功能回归、16 项按键状态机、20 项协程调度、13 项胜利记录及 32 项模拟安装检查通过，两组 Buff 通知夹具结果一致。手柄集成使用虚拟输入，未验证实体设备及自定义 Steam Input 映射；详见 [验证范围](https://github.com/wakck/MortalFastCombat/blob/v1.1.3/docs/VALIDATION-1.1.3.zh-CN.md)。

**仓库和附件均不含个人存档、胜利记录、个人配置、备份、原始日志或测试运行数据。** 测试源码与不含个人信息的验证摘要保留供审查。
