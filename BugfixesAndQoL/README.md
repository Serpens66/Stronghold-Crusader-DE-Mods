# BugfixesAndQoL

BugfixesAndQoL fixes several problems in Stronghold Crusader Definitive Edition and adds optional quality-of-life improvements. Every fix and feature can be configured in the mod settings.

## Fixes

### Restore the tannery rack fade
The enabled-by-default host fix makes the tannery rack animation fade in smoothly instead of remaining almost invisible during its first phase.

### Optional baker and miller breaks
The off-by-default host option under **Fixes?** lets bakers and all three mill workers occasionally take a positive-fear break after delivering bread or flour, even when more ingredients are available. It uses the game's existing break decision and destination. This Vanilla difference may be intentional, so the option is separate from the established fixes. Wheat and hops farmers retain their Vanilla behavior.

### Correct demolition cursor near enemies
The demolition cursor now changes to the blocked icon when an enemy is close enough to prevent demolition. This restores the visual feedback known from the HD version, so the game no longer appears to accept an action that it will reject.

### Restore HD-style minimap controls
Left-clicking the minimap can move the camera even while a building is selected for placement, without cancelling that building. While the minimap is dragged, the camera also follows the mouse position directly instead of retaining an unwanted offset. Together, these changes restore the convenient and precise minimap navigation known from the HD version.

### Cancel building placement without moving troops
In DE control mode, right-clicking while any building is selected for placement now only cancels the placement. Selected troops, including engineers placing siege tents and tunnelers placing tunnels, are no longer ordered to move to the clicked location.

### Return to the main market menu with the market hotkey
Pressing the market hotkey while a market is already selected returns its interface to the main trading menu. You no longer need to close or reselect the market after opening a sub-menu.

### Preserve the selected display resolution
In borderless fullscreen mode, the game can automatically replace the resolution loaded from `settings.cfg` during startup or focus changes. This fix preserves that resolution while still respecting display changes explicitly applied by the user.

### Fix synchronized movement for mixed troop groups
With a normal synchronized movement order, every unit in a mixed group now uses the slowest member's maximum speed and a matching animation pace. Units that can all run are no longer incorrectly forced to walk merely because their individual movement speeds differ.

### Improve hostile moat filling
This enabled-by-default host option makes units filling hostile moats choose another free, valid edge tile when the first position is occupied. If an entire moat edge is unsuitable, they continue with the next moat found by Vanilla's normal search instead of becoming idle.

### Allow cavalry movement onto stockpiles
Knights, Horse Archers, Bedouin Camel Lancers, and Heavy Camels can be ordered onto passable stockpile tiles, matching their existing ability to cross them.

### Keep Healers out of melee attack groups
When a mixed selection is ordered to attack an enemy unit, Bedouin Healers now remain in place like Engineers instead of following the combat units into melee. Normal movement orders and healing behavior remain unchanged.

### Attack through ladder-accessible walls
Vanilla can find a route over a wall with a placed ladder for an ordinary movement order, but rejects the same route while searching for attack positions. This enabled-by-default host fix applies Vanilla's own ladder checks when units attack an enemy unit or building behind such a wall.

### Resume Assassin movement after combat
Assassins resume their original movement order after automatically fighting an enemy encountered along the way, including routes that climb onto or down from walls.

### Fix plague and apothecary behavior
Each active plague outbreak now applies exactly one point of negative popularity, which is reliably removed after all associated clouds are gone. Apothecary treatments make every affected cloud fade correctly, while reserving the entire treatment area so other healers choose a different useful target. The intended building-exit transition is also completed when a target is found, preventing apothecaries from becoming stuck inside their buildings.

### Allow unrestricted rally-point placement
Barracks, mercenary posts, engineer guilds, tunneler guilds, keeps, and Bedouin tents no longer reject a rally point merely because the game considers the destination unreachable. Their rally flags can be placed anywhere the normal rally-point controls allow.

### Rotate Keep flags with their Keeps
The main flag stays in the same corner relative to the entrance when Vanilla, CastlePlanner, or Fixes rotates a human or AI Keep. This enabled-by-default host fix works in every game mode and supports Keep1, Keep2, and the larger Keep3.

### Spawn Lords on maps with corrupt Lord data
Maps that contain stale or corrupt Lord references now correctly spawn Lords for affected human and AI player slots. This includes player slots 7 and 8 on `The Ford Across the River`; valid existing Lords, loaded saves, and unaffected maps remain unchanged.

### Keep buildings away from enemy buildings and moats
This enabled-by-default host fix requires a one-tile gap around every human-placed building when an enemy completed moat is nearby. Tunnels and tunnel construction sites additionally retain the same gap from enemy buildings and walls (same like woodcutters and so on already do).

### Fix tripled starting gold in Custom Crusader Trails
In vanilla every custom trail saves with `customisedExtremeTrail=true`. When loading a mission in the Trail Maker, this results in 3 times the starting money. But when loading your trail as a normal player, you still have normal gold values. This is because the game ignores this value when starting the map, but does not ignore it in the Trail Maker. This fix makes the game also ignore it in the Trai Maker. Use my mod "StartConditions" to set up any starting gold amounts you want ;).

### Restore AI castle-defense replenishment
Vanilla can assign every newly recruited defender to the outer patrol after that patrol has first reached its target, even when later losses leave the AI short of wall defenders. This enabled-by-default host fix makes future defensive recruits refill the configured wall-defense count before the outer patrol grows again. It keeps Vanilla's existing assignment helpers and does not change units that were already assigned.

### Restore ignored AIV defender positions
Vanilla DE already reads the correct positions from user-supplied `.aivjson` files, including new AIVs for Extended Lords. Its `custom = 0` import path still skips the defensive positions in the game-provided Standard, Community, and Historical AIV sets for Pikemen, European Swordsmen, and Arabian Swordsmen. This enabled-by-default host fix removes only that remaining exclusion, allowing the existing AI defense logic to use those positions like every other supported troop row without changing the already-correct custom-AIV path.

### Improve AI wall targeting
Vanilla reserves each reachable wall segment for only one attacker at a time, which can leave the rest of an AI attack force idle until additional targets become accessible. This enabled-by-default host fix allows multiple AI attackers to target the same reachable wall segment simultaneously.

### Fix AI issues from preplaced map buildings
Preplaced buildings on maps belonging to an AI can wrongly delay an AI's castle construction (always when it is destroyed, which always happens on game start for old maps with preplaced ruins), while preplaced walls can prevent its economy from using otherwise reachable land. This enabled-by-default host fix removes the false delay, lets Vanilla's economy searches use areas reached through friendly preplaced gates, and refreshes that access after a genuine wall breach. Closed enclosures without a passable friendly gate remain blocked until they are actually opened.

### Fix AI tower rebuilding
When an AI tries to rebuild a tower from its castle plan, its own tower ruin can block the placement forever. The fix safely removes only the matching ruin owned by that AI; human, enemy, unrelated, and non-tower ruins remain untouched. (vanilla was only able to remove ruins within close range to the keep)

### Better AI overbuild rules
Stockpiles, markets, granaries, and armouries can clear ordinary obstacles while an AI builds its castle, matching the special placement behavior already used by hovels and recruitment buildings. Protected buildings and their reserved yards are preserved where AI castles overlap. If one AI demolishes a building that another AI immediately rebuilds, the repeated conflict is detected and further demolition is stopped without blocking the first legitimate overbuild attempt.

### Fix AI stone reserve mechanics
Fixed wrong stone calculations for AIs, causing it to need longer to build their castle and selling+buying stone within short time.  
Details of the bug: Vanilla gives each AI a basic stone reserve and intends to add the cost of the most expensive castle building that has not yet been built. However, it uses a value that is updated only occasionally. Early in a match this value can still be zero, causing the AI to sell stone needed for its castle and buy it back later. After construction or a failed placement, the value can instead remain outdated and make the AI hoard unnecessary stone.

### Allow an autotrade sell threshold of zero
Vanilla does not allow automatic selling when the sell slider is set to zero. The fix makes `Sell > 0` a valid setting, allowing the market to sell a good whenever any amount of it is available.

### Fix map-origin sorting
The Origin column in singleplayer and multiplayer map selection now sorts maps like intended.

### Restore map sizes for classic HD maps
Classic Stronghold Crusader HD maps now properly display their map size in the map selection list.

### Restore host migration after an abrupt disconnect
When the host leaves a running two-player match without Vanilla's normal leave packet, for example by using Alt+F4, the sole remaining human player is promoted to host. This allows the match paused by the connection error to continue and leaves Vanilla's normal player-removal flow unchanged.

### Remove disbanded units from control groups
Fix that immediately removes disbanded units from every control group. This prevents the resulting peasants, or soldiers later recruited from them, from inheriting stale group membership.

### Gate distance from center
Gatehouses now measure the distance to enemy based on their center, not based on on of the gates. It was only noticeable with small closing distance, that the clsing distance was different depending from which side you got closer to the gate.

## Quality-of-life features

### Send the nearest water carrier to each fire
The enabled-by-default host option assigns each burning building or connected building compound to one water carrier from a well or water pot. An idle carrier that is strictly closer may take over from one that is still walking, while a carrier already extinguishing the fire keeps the assignment. Other carriers choose a different reachable fire or wait.

### Improve yellow contrast
The enabled-by-default local **Improve yellow contrast** option gives yellow lobby team rows a darker gold background and replaces the yellow player's shield with a matching dark-gold version throughout the interface. It changes only that player shield: unit and building colours, minimap colours, team shields, and other yellow interface elements remain unchanged.

### Skip AI and event notifications completely
Right-click an AI or event notification video to stop its video, audio, and message text and immediately advance to the next queued notification. If the notification has no video, right-click the minimap instead. Left-clicking notification videos and normal minimap use keep their Vanilla behavior. This is an enabled-by-default per-player option.

### Pause a single production building
Hold Ctrl while clicking a production building's pause button to pause or resume only that building. Clicking without Ctrl keeps the normal behavior of changing every building of that type.

### Repair all buildings with Shift
Hold Shift while clicking a building's Repair button to repair the selected building first and then attempt every other damaged repairable building you own. Vanilla checks and deducts resources separately for every repair, so the sequence stops having an effect when the available resources are no longer sufficient.

### Queue movement and attack commands with Shift
The enabled-by-default host option extends Shift queues so movement orders and attacks against units or buildings share one deterministic FIFO with up to 128 pending commands. 

### Improve Move formations and target markers
Show correct Destination markers for moving units, even if they are a very big group. Set up how close they should stand to each other selectable from **Very dense (1)** through **Very wide (4)**.

### Make new recruits run to rally points
Newly recruited human units move to their rally points at their own normal fastest pace, with the matching animation. Terrain and other movement modifiers still apply.

### Close gates only for reachable enemies
Gatehouses can ignore enemies that cannot reach either entrance instead of closing for every nearby enemy.

### Restock siege ammunition fairly
One reload click can restock every selected catapult and trebuchet from a shared ammunition package, distributing the ammunition evenly in a way, that every unit has the same ammunition in the end. Hold Shift for five times the normal package or Ctrl for one fifth.

### Move a quarry's stone pile
Selected quarries receive a button that can move their linked stone pile clockwise to the next valid position.

### Point AI quarry piles towards their Keep
New AI quarries automatically move their linked stone pile to the valid Vanilla position nearest to that AI's Keep.

### Protect the AI economy
Four independent settings prevent affected AI production buildings from entering sleep mode when required input resources are unavailable, panic demolitions, direct deletion of living hovels, and demolitions caused solely by an inaccurate unreachable-building classification.

### Open and safely manage Vanilla maps in the map editor
The map editor's Load Map dialog includes a **Show Vanilla maps** checkbox. When enabled, it adds the editable built-in Skirmish, Free Build, and multiplayer maps to the normal list. Campaign and tutorial maps remain hidden. Saving a loaded Vanilla map always creates or overwrites a separate copy in your user `Maps` folder; the original game files are never changed.
Its **#** column shows each map's maximum player count and can be clicked to sort the list in either direction.
The Load Map and Save Map dialogs also include a **Delete Map** button. It asks for confirmation and can delete only maps stored directly in your user `Maps` folder. Vanilla maps and Steam Workshop maps are always protected from deletion.

### Customize the detailed market's goods order
The circular order of goods in the detailed market view can be rearranged freely in the mod settings. It defaults to the classic Stronghold Crusader HD order and includes a button that restores that order at any time.

### Trade exactly one market unit with Ctrl
Hold Ctrl while buying or selling at the market to trade exactly one unit instead of the normal five.

### Adjust ally goods-transfer amounts with Ctrl and Shift
In the ally goods-transfer panel, Shift multiplies the clicked amount by five and Ctrl reduces it to one fifth, while the displayed button values update to show what will be sent. After selecting a good and a positive amount, the send button remains available even if the stock changes. Vanilla rechecks the current stock when clicked; successful and insufficient transfers both keep the panel open with the selection intact, while insufficient goods also play the normal warning.

### Accept Steam lobby invitations in game
Incoming invitations can appear as a Yes/No popup ingame. An optional checkbox permanently suppresses further ingame popups for invites from that Steam user; the complete local invite blacklist can be cleared beside this feature's mod setting.

### Move the camera while holding Ctrl or Alt
Keyboard scrolling and edge scrolling now continue to move the camera while Ctrl or Alt is held.

### Jump to selected troops from the troop HUD
Middle-clicking a selected troop-type icon centers the camera on one selected unit of that type.

### Remember the selected lobby map and sorting
The shared lobby map list remembers both the last sort column and direction and the last map selected in Skirmish. This behavior is controlled by the existing **Improve selection and sorting lists** setting.

### Improve custom-lord and random-opponent selection
The custom-lord picker gains name search, sortable Name, Lord Power, and Steam Workshop origin columns, and a button that adds a random lord from the currently visible list. Random-opponent dialogs can independently use Vanilla, local, or Steam Workshop lords. The singleplayer Coop Trail also gains a scrollable AI-partner picker containing both local and Steam Workshop custom lords; all custom-lord partners intentionally share one Coop progress record. Random-AI count buttons are also available in editable multiplayer skirmish lobbies and respect the lobby, map, and human-player limits.  
In Multiplayer you can fill all 7 slots with AI.

### Include Lord JSON sidecars in Workshop uploads
While the mod is enabled, uploading a local Custom Lord or Extended CPU Lord also includes every direct `.json` file from that Lord's source folder. This supports metadata such as `info.json` and `lordmeta.json`; Vanilla continues to handle `.lordjson`, `.aivjson`, and the normal Workshop files itself. Useful to add custom descriptions for your lord see: https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/Guides/ExtendedData/CustomLordExtendedPackages.md

### Improve AIV and AIC selection
AI castle lists can be searched and sorted by origin or name, while AI configuration lists can additionally be sorted by Lord Power. Each lord's last AIV list, AIC configuration, and castle rotation is remembered across singleplayer and multiplayer lobbies, and named presets can save and restore further setups. Up to 50 ordered AIV candidates may be selected per lord; in multiplayer, additional AIV data is validated and synchronized before the match starts.

The local **Keep selected Lord after Workshop upload** option prevents successful Extended AIV Castle and Extended CPU Lord uploads from returning the upload list to the Rat.

### Improve game-speed controls
Adds Multiplayer game-speed and pause controls. Can be restricted to the host or allowed for everyone. Pressing or holding a speed key changes the speed immediately and repeats every 0.25 seconds; holding Shift changes it by 25 instead of 5 per step. The slider retains its normal 5-point increments.

### Add surrender and spectator features
Active players receive a confirmed Surrender button that kills their lord through the normal game rules, preserving the natural defeat and statistics flow. Spectators can open and refresh the current match statistics without leaving or ending the game. Eliminated players can also receive normal spectator vision and AI information.

### Identify and kick a disconnected player during resync
During a stalled multiplayer resynchronization, the host is shown the human player with the oldest overdue connection heartbeat. A confirmation button lets the host authoritatively remove that player.

### Return everyone to a multiplayer lobby after the game
After a normal multiplayer match, the host prepares a replacement lobby based on the original lobby. Every participant who is still connected joins it when leaving the final statistics with Exit, allowing the group to set up the next game together.

### Show selected-unit health in the troop HUD
The troop HUD displays current and maximum health for the selected units. Health is combined separately for each visible troop type and the current value is colored green, yellow, or red according to the remaining proportion.

### Show timer countdowns
The enabled-by-default per-player **Show timer countdowns** option adds remaining-time numbers to mission objectives and to the bars for Time Until Defeat, victory timers, and Peace Time. It changes only your display and can be turned off in the player QoL settings.

### Improve and control Assassin climbing
Assassins choose routes by expected travel time instead of treating every traversable step equally. The calculation includes normal movement speed as well as the additional time for climbing walls, so a nearby open gate can be preferred while climbing remains worthwhile along sufficiently long detours. Wall climbs can now also start and end on walkable reserved building areas, such as barracks forecourts.
When an owned Assassin is selected, a troop-action button allows or forbids climbing globally for that player's Assassins. The setting affects new path requests only. Pressing the normal Stop button or Stop hotkey while an Assassin is climbing cancels the climb now.

### Control the Lord through the troop HUD
Selecting your own Lord opens the complete troop HUD with normal commands, health display, troop-type controls, and control-group support. Disband surrenders only when the Lord is selected alone; in mixed selections it affects only normal units.

### Digging Units Get Stuck In Moat Pockets: allow units to move through allied moat
Vanilla moat-digging units can now move through completed moats owned by their player or an ally.  

### More Zoom Levels
Adds more zoom levels (with game setting Extra Zoom enabled) which allows the same zooms on every game resolution.  

### Team Markers in Statistic
On Statistic screen you will now see team markers next to the colour-shield.  

### Customize trails
Adds "Customize" Button to custom and coop trails. Allows to use custom AI as Ally in Coop Trails.  
