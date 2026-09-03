# ExtraFeatures
ExtraFeatures adds configurable gameplay, economy, building, and AI options to Stronghold Crusader Definitive Edition. The host controls the settings used for the match.

## Convenience and unit features

### Let monks run
Fighting Monks and Temple Guards can use the normal troop running behavior and animation instead of always being restricted to walking.

### Customize Lord health
Set separate health multipliers for human and AI Lords from 10% to 500%. Both current and maximum health are adjusted when the match starts, while the normal health differences between individual AI Lords are preserved.

### Mount swordsmen and dismount knights
New troop commands turn selected swordsmen into mounted knights or mounted knights back into swordsmen at the same position. Mounting requires available horses in a stable; after dismounting, the horse can either replenish normally or become available again immediately.

## Gatehouse features

### Customize automatic gate timing and distance
Set separate enemy-detection distances and reopening delays for human and AI gatehouses. Delays are measured in simulation time, so higher game speeds make the same delay pass faster in real time.

### Control gatehouse automation individually
Every owned gatehouse receives a button that switches it between normal automatic control and manual-only control. A manual-only gate no longer opens or closes automatically, remains controllable with the normal gate commands, and keeps this setting in saved games and maps.

## Buildings and production

### Add more priests to religious buildings
Churches employ two priests and cathedrals employ three instead of one. The change applies to both newly built and existing buildings.

### Customize the campfire population
Set the maximum number of peasants who may wait at the campfire, from 0 to 200, or leave the Vanilla limit unchanged.

### Customize demolition refunds
Set separate refund percentages from 0% to 100% for wood, stone, iron, pitch, and gold when buildings are demolished. Each resource can also be left at its normal Vanilla value. Wall refunds are not affected and remain at their Vanilla values because the Script Extender currently provides no supported way to adjust them.

### Preserve stored goods when demolishing storage buildings
Goods inside a granary, stockpile, or armory are returned as incoming goods when the building is demolished instead of being lost. Contents of a granary that was built for free cannot be restored, which can occur when no wood was available during construction.

### Customize enemy proximity for building actions
Set separate Singleplayer and Multiplayer enemy-exclusion radii for human and AI building actions. Human values apply to normal building placement, repair, and demolition; the demolition cursor from BugfixesAndQoL reads the same active range. AI values apply only to safely classified repairs of damaged defenses and walls and to rebuilding previously built towers and gatehouses, while initial placements and unclassified AI building calls remain unchanged.

Each value ranges from -1 to 100 fields. A value of -1 retains the complete context-specific Vanilla behavior, while 0 practically disables the proximity restriction. Normal human Vanilla checks use 30 fields in Singleplayer and 15 fields in real Multiplayer. Special placement checks with their own Vanilla radii remain unchanged. Skirmishes, Trails, and every other match that is not real Multiplayer use the Singleplayer values.

## Economy features

### Multiply gained goods
Apply separate multipliers to goods gained by human and AI players, allowing normal deposits to produce additional copies. Market purchases and demolition refunds are excluded so they are not multiplied again.

### Convert gained goods into bonus gold
Award human or AI players extra gold whenever they gain goods, based on the goods' current market sell value. This can strengthen an economy without adding more physical goods and can be combined with the goods multiplier.

### Customize market prices
Multiply all market buying and selling prices globally, then fine-tune the buying and selling multiplier of every tradeable good separately. Human players always use the configured prices; AI decisions, purchases, and sales can either use them as well or retain Vanilla prices.

## Plague features

### Customize plague-cloud duration
Set how long plague clouds remain active from 0.5 to 20 times the Vanilla duration. Longer durations also extend how long a cloud can cause damage.

### Customize apothecary search range
Set how far an apothecary searches from its assigned building for plague clouds. The range can be adjusted from 20 to 200 tiles; Vanilla uses 30.
