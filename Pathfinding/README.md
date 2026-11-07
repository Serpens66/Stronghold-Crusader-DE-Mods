# Pathfinding

Pathfinding makes units move faster, smarter and without the stutter that big armies usually cause. Large groups share one route search instead of searching one unit at a time, so AI attacks and mass orders stop freezing the game. It also collects the fixes and improvements for moving, attacking and giving orders in one place.

Every feature can be switched in the mod settings. The host decides for everyone; only the performance option is personal. All players in a multiplayer match need the same version of the mod.

The mod works in every game mode, including campaigns and the map editor.

## Less stutter with big armies

With hundreds of units, the game searches a separate route for every single soldier. That is what makes the picture freeze when an AI lord sends its army or when you order a large group.

- **Fast group and single routes** (on): groups get their routes from one shared, fast calculation, which causes far fewer and shorter freezes, even with several AI lords and thousands of units. Routes can differ slightly from Vanilla.
- **Use it for single commands too** (on): single units use the same fast search. In very crowded moments the game's own search takes over for a moment, so nothing stutters.

Loading a map takes slightly longer, because the route data is prepared in advance. If you prefer the game's own routes, turn the first option off; everything else keeps working.

## Commands

- **Improved manual unit commands** (on): in a mixed selection, the units that can reach the target follow your move, patrol, attack and moat orders, even if others cannot. Units already standing in a moat can follow them too.
- **Attack through ladder-accessible walls** (on): troops use existing ladders when you order an attack on a target behind a wall.
- **Unit lineup and target markers** (on): choose lineup, density and where ranged and siege units stand from the troop panel, and drag over open ground to set a line. Your units do not march in formation; they take up their places when they arrive. Long moves show complete target markers.
- **Extended Shift command queues** (on): queue up to 128 move and attack orders with Shift. All destinations are shown on the map, also across several pages.

## Moats

- **Improved moat filling** (on): when the first spot is taken, units pick another free spot along the moat instead of standing around.
- **Units cross own and allied moats** (off): units that can enter moats may cross finished moats of their own side when that way is cheaper. This changes how battles play out, so it is off by default. It needs the fast routes.

## Gates

- **Avoid hostile gatehouse passages** (on): players and AI no longer plan ground routes through enemy gatehouses they cannot use. Your own, allied and properly captured gates, permitted Assassin climbing and gate attacks are unaffected.
- **Close only for reachable enemies** (on): an enemy nearby only closes a gatehouse automatically if it can actually walk to one of its two entrances.

## Assassins

- **Improved Assassin pathfinding** (on, separately for human players and AI lords): Assassins choose routes by expected travel time, including climbing. Human players get a button to allow or forbid climbing.
- **Assassins can climb gatehouses directly** (on): humans and AI can choose a direct climb onto a gatehouse, using the normal climbing animation.
- **Resume Assassin movement after combat** (on): Assassins continue their original order after a melee fight they got drawn into, including routes that require climbing.

## Troop movement

- **Troop speed fix** (on): with a normal move order, mixed groups move at the speed of their slowest unit and stay together.
- **Fix rally point terrain slowdown** (on): units on their way to a rally point are slowed by swamps, fords and climbing as usual.
- **Recruits run to rally points** (on): newly recruited units of human players run to their rally point at their normal running speed. AI units are unchanged.
- **Mounted stockpile movement fix** (on): cavalry can be ordered onto stockpile tiles they can already pass.
- **Rally point placement fix** (on): rally point flags can be placed everywhere.

## Fire

**Avoid fire** (off) needs the fast routes. Routes lead around burning pitch and other fire on the ground. Soldiers that are already walking replan shortly before new fire or wait until the way is free. If no safe route appears within the waiting time, the move is cancelled, so nobody walks through the flames.

- **Soldiers leave burning tiles** (on): soldiers standing in fire walk to the nearest safe tile and later return or continue their move. Workers, firemen, siege equipment and units on walls stay where they are.
- **Waiting time** (60 seconds of game time) and **return window** (120 seconds): how long soldiers wait in front of fire and how long they may still return afterwards.
- **Allowed detour** (50 %, at least 10 tiles): a safe route may be this much longer. Longer detours count as no route. This also applies to plague.

## Plague

**Avoid plague** (off) needs the fast routes. Routes lead around tiles where a plague cloud would really damage the unit. Soldiers replan, wait or cancel their move as with fire. Removing a cloud with an apothecary clears the way at once.

- **Soldiers leave plague** (on): soldiers being damaged walk to the nearest safe tile and later return. The Lord and the healer are immune and stay.
- **Also leave walls, gates, towers and keeps** (off): without this option, the garrison stays at its post.
- **Waiting time** (90 seconds of game time) and **return window** (180 seconds).

## Performance

**Vanilla performance optimizations** (on, personal): checks group membership in one pass instead of repeating it for every member. The result is identical to Vanilla.

## Good to know

- The mod needs APIShared and the Script Extender.
- Pathfinding currently cannot run together with BugfixesAndQoL or MoatMove, because they contain the same features. If one of them is installed, Pathfinding stays inactive.
- Fire and plague avoidance only affects routes the fast search calculates. In the rare moments when the game's own search takes over, a new order can lead past a hazard; soldiers still replan before they reach it.
- Soldiers waiting in front of fire or plague keep waiting after you save and load.
