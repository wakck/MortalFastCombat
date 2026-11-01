# 跳过战斗快捷键（1.1.3）

默认键盘 **F10**，手柄 **R3（按下右摇杆）**。战斗左上角的“跳过并获胜”按钮会显示当前按键。

快捷键与原按钮使用相同的资格检查和本场胜利结算：仅同周目已真正击败的相同普通敌人、首条指令前可用。剧情战、暂停、输入文字、模组设置或助手弹窗打开时不可用。不需要开启快速战斗。

按一次即可，持续按住不会重复结算。切换场景、恢复暂停或关闭弹窗后，需要先松开按键再按。R3 在无法跳过时保留游戏原来的界面选择功能。

退出游戏后编辑 `BepInEx/config/local.legendofmortal.fastcombat.cfg`：

```ini
[Hotkeys]
EnableSkipHotkeys = true
SkipKeyboardKey = F10
EnableSkipGamepadHotkey = true
SkipGamepadButton = RightStick
```

- `EnableSkipHotkeys = false`：关闭所有跳过快捷键，鼠标按钮继续可用。
- `SkipKeyboardKey = None`：只关闭键盘快捷键；也可填 `F11` 等 Unity Input System 键名。
- `EnableSkipGamepadHotkey = false`：只关闭手柄快捷键。
- 手柄常用值：`RightStick`（R3）、`LeftStick`（L3）、`South`（Xbox A / PS 叉）、`North`（Xbox Y / PS 三角）、`LeftShoulder`（LB / L1）、`RightShoulder`（RB / R1）。推荐保留 R3，避免与出招或对话键重叠。

LOMAssistant 3.3.2 默认按 L3 打开助手面板，可与本模组默认 R3 共存。如果将助手 `GamepadPanelButton` 改成 `1`（R3），应修改其中一方，避免同一个按键打开助手并跳过战斗。Steam Input 应输出普通键盘或手柄按钮；模组读取 Unity Input System 的设备状态。

本次只增加快捷键与按键提示，不改变敌人识别、胜利记录格式、奖励及倍速逻辑。隔离测试使用虚拟 Keyboard / Gamepad 事件；实际手柄型号及 Steam Input 自定义映射仍需在设备上确认。
