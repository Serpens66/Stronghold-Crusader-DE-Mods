# BugfixesAndQoL

BugfixesAndQoL fixes several problems in Stronghold Crusader Definitive Edition and adds optional quality-of-life improvements. Every fix and feature can be configured in the mod settings.

## Fixes

### Correct demolition cursor near enemies
The demolition cursor now changes to the blocked icon when an enemy is close enough to prevent demolition. This restores the visual feedback known from the HD version, so the game no longer appears to accept an action that it will reject.

### Restore HD-style minimap controls
Left-clicking the minimap can move the camera even while a building is selected for placement, without cancelling that building. While the minimap is dragged, the camera also follows the mouse position directly instead of retaining an unwanted offset. Together, these changes restore the convenient and precise minimap navigation known from the HD version.

### Return to the main market menu with the market hotkey
Pressing the market hotkey while a market is already selected returns its interface to the main trading menu. You no longer need to close or reselect the market after opening a sub-menu.

### Preserve the selected display resolution
In borderless fullscreen mode, the game can automatically replace the resolution loaded from `settings.cfg` during startup or focus changes. This fix preserves that resolution while still respecting display changes explicitly applied by the user.

### Fix synchronized movement for mixed troop groups
With a normal synchronized movement order, every unit in a mixed group now uses the slowest member's maximum speed and a matching animation pace. Units that can all run are no longer incorrectly forced to walk merely because their individual movement speeds differ.

### Fix plague and apothecary behavior
Each active plague outbreak now applies exactly one point of negative popularity, which is reliably removed after all associated clouds are gone. Apothecary treatments make every affected cloud fade correctly, while reserving the entire treatment area so other healers choose a different useful target. The intended building-exit transition is also completed when a target is found, preventing apothecaries from becoming stuck inside their buildings.

### Allow unrestricted rally-point placement
Barracks, mercenary posts, engineer guilds, tunneler guilds, keeps, and Bedouin tents no longer reject a rally point merely because the game considers the destination unreachable. Their rally flags can be placed anywhere the normal rally-point controls allow.

### Fix tripled starting gold in Custom Crusader Trails
The game can interpret the unusable `customisedExtremeTrail` value in a `.trail` file as a request to triple the mission's starting gold. The fix ignores that value when a trail is loaded through the Trail Maker or Customize screen and writes a safe value when a trail is newly saved or resaved; existing files are not modified until they are saved.

### Stop failed knight recruitment from wasting AI resources
If an AI has the equipment for a knight but no horse is available, Vanilla can accidentally reuse an older missing-weapon result. The AI then repeatedly buys and sells equipment it does not need; this fix recognizes the horse-only shortage and prevents that cycle.

### Fix AI tower rebuilding
When an AI tries to rebuild a tower from its castle plan, its own tower ruin can block the placement forever. The fix safely removes only the matching ruin owned by that AI; human, enemy, unrelated, and non-tower ruins remain untouched.

### Better AI overbuild rules
Stockpiles, markets, granaries, and armouries can clear ordinary obstacles while an AI builds its castle, matching the special placement behavior already used by hovels and recruitment buildings. Protected buildings and their reserved yards are preserved where AI castles overlap. If one AI demolishes a building that another AI immediately rebuilds, the repeated conflict is detected and further demolition is stopped without blocking the first legitimate overbuild attempt.

### Fix AI stone reserve mechanics
Vanilla gives each AI a basic stone reserve and intends to add the cost of the most expensive castle building that has not yet been built. However, it uses a value that is updated only occasionally. Early in a match this value can still be zero, causing the AI to sell stone needed for its castle and buy it back later. After construction or a failed placement, the value can instead remain outdated and make the AI hoard unnecessary stone.

The fix reads the current castle plan whenever the AI is about to sell stone. The AI keeps its configured base reserve plus the current stone cost of the most expensive ordinary building still waiting for its first successful construction. The extra reserve disappears immediately after that building is placed or its placement fails, and it is not restored merely because a completed building is later destroyed.

Walls, crenellations, stairs, moats, pitch areas, and other multi-part castle commands are excluded. If several normal buildings qualify, only the most expensive one determines the additional reserve; their costs are not added together.

### Allow an autotrade sell threshold of zero
Vanilla does not correctly enable automatic selling when the sell slider is set to zero. The fix makes `Sell > 0` a valid setting, allowing the market to sell a good whenever any amount of it is available.

### Fix map-origin sorting
The Origin column in singleplayer and multiplayer map selection now sorts maps into reversible Vanilla, local, and Steam Workshop groups. Unknown or malformed entries remain safely at the end of the list.

## Quality-of-life features

### Customize the detailed market's goods order
The circular order of goods in the detailed market view can be rearranged freely in the mod settings. It defaults to the classic Stronghold Crusader HD order and includes a button that restores that order at any time.

### Trade exactly one market unit with Ctrl
Hold Ctrl while buying or selling at the market to trade exactly one unit instead of the normal five. Ctrl+Shift deliberately restores the normal five-unit trade.

### Adjust ally goods-transfer amounts with Ctrl and Shift
In the ally goods-transfer panel, Shift multiplies the clicked amount by five and Ctrl reduces it to one fifth. Holding both modifiers uses the normal amount, and the displayed button values update to show what will be sent.

### Accept Steam lobby invitations in game
Incoming invitations can appear as a Yes/No prompt only after Steam confirms that the sender is a current friend and successfully resolves the invited lobby. Accepting uses the game's normal leave-and-join flow. When declining, an optional checkbox permanently suppresses further mod prompts from that Steam user; the complete local invite blacklist can be cleared beside this feature's mod setting. This validation affects only the mod's popup and never filters, rejects, or changes Steam's invitation or overlay handling. Every invitation for which the mod popup is suppressed is recorded as a warning with its exact validation reason in the BepInEx log.

### Move the camera while holding Ctrl or Alt
Keyboard scrolling and edge scrolling continue to move the camera while Ctrl or Alt is held. This prevents modifier keys used for other controls from unnecessarily locking camera movement.

### Improve custom-lord and random-opponent selection
The custom-lord picker gains name search, sortable Name, Lord Power, and Steam Workshop origin columns, and a button that adds a random lord from the currently visible list. Random-opponent dialogs can independently use Vanilla, local, or Steam Workshop lords. Random-AI count buttons are also available in editable multiplayer skirmish lobbies and respect the lobby, map, and human-player limits.

### Improve AIV and AIC selection
AI castle lists can be searched and sorted by origin or name, while AI configuration lists can additionally be sorted by Lord Power. Each lord's last AIV list, AIC configuration, and castle rotation is remembered across singleplayer and multiplayer lobbies, and named presets can save and restore further setups. Up to 50 ordered AIV candidates may be selected per lord; in multiplayer, additional AIV data is validated and synchronized before the match starts. Missing files in a saved preset are handled safely.

### Improve game-speed controls
Multiplayer game-speed and pause controls can be disabled, restricted to the host, or allowed for everyone. Authorized players can pause and continue the match with the normal pause key and use the normal speed keybinds or options slider for game-speed changes. Pressing or holding a speed key changes the speed immediately and repeats every 0.25 seconds; holding Shift changes it by 25 instead of 5 per step. The slider retains its normal 5-point increments, and multiplayer changes do not overwrite the saved singleplayer speed.

### Add surrender and spectator features
Active players receive a confirmed Surrender button that kills their lord through the normal game rules, preserving the natural defeat and statistics flow. Spectators can open and refresh the current match statistics without leaving or ending the game. Eliminated players can also receive normal spectator vision and AI information while their player slot, team membership, synchronized state, and final statistics remain unchanged.

### Identify and kick a disconnected player during resync
During a stalled multiplayer resynchronization, the host is shown the human player with the oldest overdue connection heartbeat. A confirmation button lets the host authoritatively remove that player; no player is suggested while all connections remain current.

### Return everyone to a multiplayer lobby after the game
After a normal multiplayer match, the host prepares a replacement lobby based on the original lobby. Every participant who is still connected joins it when leaving the final statistics with Exit, allowing the group to set up the next game together.

### Show selected-unit health in the troop HUD
The troop HUD displays current and maximum health for the selected units. Health is combined separately for each visible troop type and the current value is colored green, yellow, or red according to the remaining proportion.

### Improve and control Assassin climbing
Assassins choose routes by expected travel time instead of treating every traversable step equally. The calculation includes normal movement speed as well as the additional time for climbing normal walls, low walls, stairs, and downward transitions, so a nearby open gate can be preferred while climbing remains worthwhile along sufficiently long detours. Wall climbs can also start and end on walkable reserved building areas, such as barracks forecourts.

When an owned Assassin is selected, a troop-action button allows or forbids climbing globally for that player's Assassins. The setting affects new path requests only; ordinary stairs and already accessible wall surfaces remain usable. AI Assassins always retain climbing. Pressing the normal Stop button or Stop hotkey while an Assassin is climbing cancels the climb, clears the current movement order, and makes the unit immediately controllable again.

### Control the Lord through the troop HUD
Selecting your own Lord opens a compact version of the normal troop HUD with stance and unit commands as well as optional health values. The disband command is replaced by the same confirmed surrender action, preventing an accidental or unsynchronized Lord removal.
