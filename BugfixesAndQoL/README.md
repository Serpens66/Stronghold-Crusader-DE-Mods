# BugfixesAndQoL

**Fixes**:
- Fixes the missing demolition cursor near enemy units (like in HD version)
- Allows the minimap to be used while placing a building (like in HD version)
- Makes the camera follow the cursor directly while dragging on the minimap (like in HD version)
- Pressing the market hotkey while the market is already open returns to the main trading menu (like in HD version)
- Prevent the game from sometimes resetting game resolution back to standard
- Adds a freely customizable market goods order, defaulting to the classic HD order
- Fixes synchronized movement for mixed troop groups, so every unit move at the speed of the slowest unit (in vanilla eg. archers and slaves walk slow together, although both can run)
- Fixes plague popularity penalties and reliably removes them after the plague has been eliminated
- Prevents treated plague clouds from becoming active again
- Fixes apothecaries getting stuck when leaving their buildings
- Allows rally points to be placed anywhere.
- Fixes incorrectly tripled starting gold in custom Crusader Trails
- Fixed AI to not waste money on endless buy/sell when no horses are available to recruit knights

### Fixed AI stone reserve mechanics:
What Vanilla does
Each AI has a configured base amount of stone it tries to keep. Stone receives a special additional reserve intended for the most expensive castle building that has not yet been built.
The problem is that Vanilla does not calculate this reserve directly when deciding whether to sell stone. It uses an auxiliary value that is refreshed by other AI routines only occasionally.
This produces two separate problems:
1. At the beginning of a match, the auxiliary value is often still zero. The AI therefore sells stone above its normal base amount, even though its castle plan already contains an expensive tower that still needs to be built.
2. Much later, Vanilla may finally discover that tower and add its cost to the reserve. That value can then remain outdated after the castle has been completed or when the tower cannot be placed. The AI continues hoarding unnecessary stone and clutters its small stockpile.
This also explains the seemingly irrational sell-and-buy cycle: the seller does not yet know about the upcoming building, sells the starting stone, and the construction system later has to buy it back.
What the fix does
Whenever the AI is about to sell stone, the fix reads the current castle plan directly. There is no delayed scan and no cached reserve.
The resulting behaviour is:
- When the castle plan is created, every normal building waiting for its first construction is immediately considered.
- The AI keeps its configured base amount plus the current stone cost of the most expensive such building.
- If the AI lacks the resources to construct that building, it remains eligible for the reserve. The AI therefore does not sell the stone it is still accumulating for it.
- Once the building has been placed successfully for the first time, its additional reserve disappears at the next normal selling decision.
- If placement fails, the additional reserve also disappears. A permanently blocked tower therefore cannot cause permanent stone hoarding.
- If an already completed building is later destroyed, rebuilding it does not reactivate the additional reserve.
- Once no relevant first-time castle building remains, the extra reserve is immediately zero.
Only ordinary buildings with a positive current stone cost qualify. Walls, crenellations, stairs, moats, pitch areas and other multi-part castle commands are excluded. If several buildings qualify, their costs are not added together; only the most expensive one determines the reserve.














**QoL**:
- Change Multiplayer Speed midgame.
- Hold Ctrl while trading to buy or sell exactly 1 unit of a good
- Hold Ctrl/Shift while sending goods to send even less/more goods
- Ingame Popup for multiplayer invites, to join without tabbing out
- Allows camera movement while Ctrl or Alt is held
- Improves apothecary target reservation so multiple apothecaries do not treat the same plague area
- Remembers each AI lord’s selected AIV list, AIV rotation, and custom AIC settings across singleplayer and multiplayer lobbies
- Allows automatic selling with a sell threshold of zero
- Allow random custom lords in the randomized skirmish setup
- Add sorting and filtering for the skirmish custom lord selection list
- Random AIs button in multiplayer
- Allow Surrender and to view statistics for spectators
- Button to Kick players who lost connection
- Show HP number of units and control for the lord unit





