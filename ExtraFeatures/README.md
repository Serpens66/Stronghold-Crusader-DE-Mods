# ExtraFeatures

ExtraFeatures adds configurable gameplay, economy, building, and AI options to Stronghold Crusader Definitive Edition. The host controls the settings used for the match.

## Convenience and unit features

### Pause a single production building
Hold Ctrl while clicking a production building's pause button to pause or resume only that building. Clicking without Ctrl keeps the normal behavior of changing every building of that type.

### Make new recruits run to rally points
Newly recruited human and AI units move to their rally points at their own normal fastest pace, with the matching animation. Terrain and other movement modifiers still apply.

### Let monks run
Fighting Monks and Temple Guards can use the normal troop running behavior and animation instead of always being restricted to walking.

### Customize Lord health
Set separate health multipliers for human and AI Lords from 10% to 500%. Both current and maximum health are adjusted when the match starts, while the normal health differences between individual AI Lords are preserved.

### Mount swordsmen and dismount knights
New troop commands turn selected swordsmen into mounted knights or mounted knights back into swordsmen at the same position. Mounting requires available horses in a stable; after dismounting, the horse can either replenish normally or become available again immediately.

## Gatehouse features

### Customize automatic gate timing and distance
Set separate enemy-detection distances and reopening delays for human and AI gatehouses. Delays are measured in simulation time, so higher game speeds make the same delay pass faster in real time.

### Close gates only for reachable enemies
Gatehouses can ignore enemies that cannot reach either entrance instead of closing for every nearby enemy. If reachability cannot be checked safely, the normal game behavior is retained.

### Control gatehouse automation individually
Every owned gatehouse receives a button that switches it between normal automatic control and manual-only control. A manual-only gate no longer opens or closes automatically, remains controllable with the normal gate commands, and keeps this setting in saved games and maps.

## Buildings and production

### Move a quarry's stone pile
Selected quarries receive a button that moves their linked stone pile clockwise to the next valid position. If no replacement can be placed safely, the existing pile remains untouched.

### Add more priests to religious buildings
Churches employ two priests and cathedrals employ three instead of one. The change applies to both newly built and existing buildings.

### Customize the campfire population
Set the maximum number of peasants who may wait at the campfire, from 0 to 200, or leave the Vanilla limit unchanged.

### Customize demolition refunds
Set separate refund percentages from 0% to 100% for wood, stone, iron, pitch, and gold when buildings are demolished. Each resource can also be left at its normal Vanilla value.

### Preserve stored goods when demolishing storage buildings
Goods inside a granary, stockpile, or armory are returned as incoming goods when the building is demolished instead of being lost. Contents of a granary that was built for free cannot be restored, which can occur when no wood was available during construction.

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

## AI behavior

### Customize the safe distance for AI castle repairs
Set how close an enemy may be before the AI stops repairing standing defenses or rebuilding previously built towers and gatehouses. The Vanilla rule can be retained, while a value of 0 practically removes this proximity restriction.

### Prevent AI production pauses
Stop AI players from putting their own production buildings to sleep, keeping those buildings active.

### Prevent AI panic demolition
Disable the AI's emergency resource-recovery demolition routine so it cannot remove otherwise useful buildings while under economic pressure.

### Prevent AI hovel deletion
Stop the AI from directly deleting its own living hovels for economic reasons. Hovels can still be destroyed normally by damage.

### Protect AI buildings classified as unreachable
Choose between unchanged Vanilla behavior, an improved reachability check, or blocking every demolition caused solely by unreachability. The improved check treats living friendly and allied gatehouses and their drawbridges as passable regardless of their current state, while all unrelated demolition reasons remain unchanged.
