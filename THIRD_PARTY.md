# Third-party components

The v1.1.3 LOMAssistant coexistence package distributes MortalFastCombat's plugin and
maintenance files only. It does **not** redistribute LOMAssistant, BepInEx 6, HarmonyX,
Unity, or game binaries. Install the reviewed LOMAssistant 3.3.2 / BepInEx 6 environment
separately, under the upstream project's applicable terms.

MortalFastCombat references BepInEx 6 and HarmonyX APIs, and reads local Unity/game
assemblies for compilation. Source/build references do not transfer upstream licenses
to this project's own source. No separate project source license has been assigned.

Upstream projects:

- LOMAssistant: https://github.com/kevinnie2003/LOMAssistant
- BepInEx: https://github.com/BepInEx/BepInEx
- HarmonyX: https://github.com/BepInEx/HarmonyX

The historical v1.1.0 runtime ZIP retained in `release/` contains unmodified official
BepInEx 5.4.23.6 / UnityDoorstop 4.6.0 binaries. Its notices remain in `licenses/` and
inside that ZIP: BepInEx, HarmonyX, MonoMod and Mono.Cecil (MIT); UnityDoorstop (LGPL-2.1).
Those notices apply to their corresponding components, not to the v1.1.3 plugin.
