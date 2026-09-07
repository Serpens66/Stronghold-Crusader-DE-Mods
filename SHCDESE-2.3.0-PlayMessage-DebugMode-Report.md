# `PlayMessage` depends on a debug-only detour

## Affected version

- SHCDE Script Extender 2.3.0
- Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`

## Problem

`GamePlayerManagerAPI.PlayMessage` directly calls `BulkAIDetours.c_game_ai_enqueue_message_wrapper_hook_impl`. That implementation forwards through `c_game_ai_enqueue_message_wrapper_hook.Original(...)`.

However, the wrapper detour is installed in `BulkAIDetours.Install` only when `Plugin.Instance.DebugMode.Value` is true. With the normal debug setting disabled, the detour handle therefore appears not to receive an original-function trampoline before the public C# and Lua API can call it.

Relevant locations:

- `src/SHCDESE.BepInEx/API/GamePlayerManagerAPI.cs`, `PlayMessage`
- `src/SHCDESE.BepInEx/Detours/BulkAIDetours.cs`, debug-gated wrapper installation and `c_game_ai_enqueue_message_wrapper_hook_impl`

## Expected behavior

The public `PlayMessage` API should work independently of the diagnostic debug-mode setting.

## Suggested fix

Install the wrapper detour unconditionally, while keeping only its verbose logging conditional, or expose a separately resolved callable original delegate that does not depend on installing the diagnostic detour. A regression test should call `PlayMessage` with debug mode disabled and verify that the native wrapper is invoked without an uninitialized-detour failure.
