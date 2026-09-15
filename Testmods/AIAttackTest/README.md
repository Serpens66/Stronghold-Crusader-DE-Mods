# AI Attack Test

AI Attack Test is an experimental synchronized gameplay mod for Stronghold Crusader Definitive Edition. It exposes selected UCP-inspired AI attack and recruitment behavior as host-controlled lobby settings.

## Features

### Lord-relative attack growth

In `Relative` mode, every active AI lord receives attack-wave growth based on that lord's own `siege_trigger_level`:

- Normal growth step: `siege_trigger_level × configured percentage`
- High-gold growth step: normal growth step × `7 / 5`
- Results use conventional half-up rounding

The setting is shared by all AI players, but the result remains lord-dependent because every lord keeps its own trigger value.

The mod does not replace `siege_max_troops`. Each lord retains the individual troop cap defined by its AIC/lordjson configuration, so high growth percentages may reach that cap sooner.

### Attack the enemy lord after a breach

Vanilla normally limits the post-breach lord attack command to the first eligible AI tribe. When this option is enabled, every eligible tribe may attempt to attack the enemy lord.

The existing Vanilla checks remain in place, including unit-role eligibility, a living target lord, UID validation, movement failure handling, and fallback behavior.

### Configurable initial defense-only period

The initial period during which the AI recruits defenders instead of progressing normally can be configured from 0 to 30 in-game months.

- `0` months removes the initial delay.
- `6` months matches the Vanilla value.
- One in-game month corresponds to 800 simulation ticks.

## Settings

All gameplay settings are synchronized, controlled by the host, and applied equally to all active AI players.

- `Enable Mod` — Enables or disables all gameplay changes. Default: enabled.
- `Attack Scaling Mode` — Selects `Vanilla` or `Relative` attack growth. Default: `Relative`.
- `Relative Attack Growth Percent` — Sets relative wave growth from 0% to 300% in steps of 10%. Default: 50%.
- `Attack Lord After Breach` — Allows every eligible AI tribe to receive the post-breach lord attack command. Default: enabled.
- `Initial Defense-Only Months` — Sets the initial defender-recruitment period from 0 to 30 months. Default: 0.

## Not included yet

- AiWallTargetingFix is already in out BugfixesAndQoL mod.  
- The possible UCP-style 4/2/1 target rotation—four wall targets, two fortification targets, and one building target—is intentionally not part of this version. It requires an additional Definitive Edition native-code audit before it can be implemented safely. See `FUTURE_WORK.md` for details.
