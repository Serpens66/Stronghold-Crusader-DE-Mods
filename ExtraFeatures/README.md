# ExtraFeatures
ExtraFeatures adds configurable gameplay, economy, building, and AI options to Stronghold Crusader Definitive Edition. The host controls the settings used for the match.

## Convenience and unit features

### Disable fear-factor effects on soldiers
An optional host setting removes both the fear-factor damage bonus and penalty for all human and AI soldiers. Fear values, popularity and productivity effects remain unchanged. Disabled by default; also works in the map editor when ExtraFeatures and this option are enabled.

### Build moats on elevated terrain
Separate host checkboxes allow AI and/or human players to create moats and drawbridges on elevated terrain. AI placement is enabled by default, while human placement is disabled. If either checkbox is enabled, direct placement in the map editor and filling, cancelling, or removing any moat remain available to everyone.

### Let monks run
Disabled by default: Fighting Monks and Temple Guards can use the normal troop running behavior and animation instead of always being restricted to walking.

### Customize Lord health
Set separate health multipliers for human and AI Lords from 10% to 500%. Both current and maximum health are adjusted when the match starts, while the normal health differences between individual AI Lords are preserved.

### Mount swordsmen and dismount knights
New troop commands turn selected swordsmen into mounted knights or mounted knights back into swordsmen at the same position. Mounting requires available horses in a stable; after dismounting, the horse can either replenish normally or become available again immediately.

### Let Bedouin healers heal civilians and siege engines
Two independent host options allow Bedouin healers to heal wounded civilians and siege engines belonging to human players. Both are disabled by default. Soldiers continue to be healed as in the base game.

## Gatehouse features

### Customize automatic gate timing and distance
Set separate enemy-detection distances and reopening delays for human and AI gatehouses.

### Control gatehouse automation individually
Every owned gatehouse receives a button that switches it between normal automatic control and manual-only control. A manual-only gate no longer opens or closes automatically.

## Buildings and production

### Repair more buildings
By default, owned buildings with a building panel get a small hammer button above the help button. Click it to repair a damaged building. The tooltip shows the required resources and what you have; repairs can also use iron, pitch, and gold when the building costs include them. The host can turn this feature off in ExtraFeatures settings. With BugfixesAndQoL installed, Shift-clicking the repair button can repair all eligible buildings.

### Add more priests to religious buildings
Churches employ two priests and cathedrals employ three instead of one.

### Customize the campfire population
Set the maximum number of peasants who may wait at the campfire, from 0 to 200, or leave the Vanilla limit unchanged.

### Customize demolition refunds
Set separate refund percentages from 0% to 100% for wood, stone, iron, pitch, and gold when buildings are demolished. Each resource can also be left at its normal Vanilla value. Wall refunds are not affected and remain at their Vanilla values because the Script Extender currently provides no supported way to adjust them.

### Preserve stored goods when demolishing storage buildings
Goods inside a granary, stockpile, or armory are returned as incoming goods when the building is demolished instead of being lost. Contents of a granary that was built for free cannot be restored, which can occur when no wood was available during construction.

### Customize enemy proximity for building actions
Set separate Singleplayer and Multiplayer enemy-exclusion radii for human and AI building actions. Human values apply to normal building placement, repair, and demolition; the demolition cursor from BugfixesAndQoL reads the same active range. AI values apply only to safely classified repairs of damaged defenses and walls and to rebuilding previously built towers and gatehouses, while initial placements and unclassified AI building calls remain unchanged.

## Economy features

### Customize victory loot
The host can configure gold and goods percentages and an absolute gold limit independently for human and AI winners. The settings apply when the winner defeats an enemy Lord and can be changed between matches.

Both percentage sliders range from -1 to 100 in steps of 1. All six settings default to -1, preserving Vanilla rewards:

| Setting | -1: Vanilla | Custom value |
|---|---|---|
| Gold share | 25% of the defeated player's gold, at least 500 before applying the gold limit | 0-100% of the defeated player's gold, without a minimum |
| Goods share | 25% of each eligible goods stock | 0-100% of each eligible goods stock |
| Gold limit | At most 100,000 gold per defeated Lord | Any positive whole number sets the absolute limit |

Percentages are rounded down, separately for each goods type. A percentage of 0 gives no reward of that kind. The gold limit applies after calculating the gold share, including when the share is left at Vanilla: a limit below 500 overrides the Vanilla minimum, and a larger limit can allow rewards above 100,000. Goods have no separate loot limit. Gold is credited immediately; goods enter the incoming-goods queue. The victory message remains even when both shares are 0.

Existing profiles and Trails that enabled **No Kill Reward** automatically use 0% for both gold and goods for the affected winner role. Migration happens when settings are loaded, before editing. Human and AI settings remain independent, and subsequent changes are preserved in presets and multiplayer synchronization.

Version 1.0.125 fixes excessive work while moving these sliders and a native statistic-address error that caused exceptions and Vanilla loot fallback when a Lord died.

### Gold every 400 ticks
Set separate whole-number gold amounts for human and AI players in the Economy settings. Both default to 0; positive and negative values are supported. The host chooses these settings between matches, and they are included in presets and multiplayer synchronization. The Active/Inactive search filter treats 0 as inactive and either sign as active.

Every 400 simulation ticks (about 10 seconds at game speed 40), living players receive their configured amount. Positive amounts enter the normal incoming-gold queue and are credited gradually; negative amounts immediately reduce stored gold, stopping at 0 and leaving pending incoming gold intact. Positive amounts are limited to the remaining capacity of the stored and queued gold balances.

Payments follow the saved simulation tick: 400, 800, 1200, and so on. Starting or loading a map never triggers an immediate or catch-up payment. The same rules apply in the map editor when the simulation is running and living players exist. When both amounts are 0, the gold tick callback is unsubscribed.

### Customize popularity
Adjust popularity values independently for human and AI players in the Popularity section of the mod settings.

### Multiply gained goods
Apply separate multipliers to goods gained by human and AI players, allowing normal deposits to produce additional copies.

### Convert gained goods into bonus gold
Award human or AI players extra gold whenever they gain goods, based on the goods' current market sell value. This can strengthen an economy without adding more physical goods and can be combined with the goods multiplier.

### Customize market prices
Multiply all market buying and selling prices globally, then fine-tune the buying and selling multiplier of every tradeable good separately. Human players always use the configured prices; AI decisions, purchases, and sales can either use them as well or retain Vanilla prices.

## Plague features

### Plague damage on elevated defenses
Plague clouds can also damage units on nearby walls, gates, towers and keeps when their heights are close enough. The host controls each of these four surfaces separately: walls, gates and towers are enabled by default, keeps are disabled; normal damage and apothecary protection still apply.

### Customize plague-cloud duration
Set how long plague clouds remain active from 0.5 to 20 times the Vanilla duration. Longer durations also extend how long a cloud can cause damage. By default set to *4.

### Customize apothecary search range
Set how far an apothecary searches from its assigned building for plague clouds. The range can be adjusted from 20 to 200 tiles; Vanilla uses 30.

## Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.16.1 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games.

Plague-height, market-price, repair-HUD and troop-command integrations use coordinated APIShared hooks. Gatehouse and mount/dismount actions use its synchronized multiplayer transport.

Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change.
