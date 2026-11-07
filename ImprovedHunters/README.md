# ImprovedHunters

Makes Hunters more useful and gives Tanneries a second purpose. Hunters choose their prey sensibly, also hunt rabbits, camels and your own chickens, collect free carcasses and find their way around obstacles. Tanneries can turn cows into meat. Everything can be set in the mod settings.

## Hunters

### What they hunt and how much meat it gives
You decide separately for deer, goats, rabbits, camels and chickens whether Hunters hunt them and how much meat one animal gives (0 to 100).

| Animal | Hunted by default | Base meat |
|---|---|---|
| Deer | Yes | 6 |
| Goat | Yes | 6 |
| Rabbit | Yes | 2 |
| Camel | Yes | 8 |
| Chicken | Yes, only your own | 1 |

The meat value is a base amount. The game's usual production bonus still applies, as for all production: +50% in skirmish and multiplayer, more with fear factor. A deer therefore brings 9 meat in a normal skirmish.

### Better choice of prey
With **Improved Target Selection**, Hunters prefer the animal that brings the most meat for the time it takes, including the walk there and back to the granary. They ignore animals they cannot reach. A Hunter who is already on his way changes his mind once if a clearly closer animal appears, and does not change back.

### Better pursuit
With **Improved Pathfinding**, Hunters walk around walls and buildings until they have a clear shot, keep following an animal that runs away, and speed up or slow down according to the way that is actually left. If an animal becomes unreachable on the way, the Hunter gives it up and looks for another one.

### Free carcasses
Hunters also collect free carcasses, such as animals killed by someone else, when that pays off more than hunting. This applies to the animals you enabled above. A carcass another Hunter has claimed stays his, and if a Hunter's arrow kills a different animal than the one he aimed at, he collects that one. With this option off, Hunters only take deer and goat carcasses after one of their own shots, as in the base game. On by default.

### Search range
Limits how far Hunters look for prey, from 21 to 200 tiles. 0 means no limit, as in the base game. The limit applies to all Hunters, including those of the AI.

### Hunter's hut always available
In the base game, the Hunter's hut cannot be built in skirmish and multiplayer when the map starts without deer and goats. With this option, which is on by default, you can always build it, for example for chickens, rabbits and camels.

## Animals

### Deer and goats flee only from units in sight
In the base game, a herd runs from any unit in its area, even one behind a wall. With this option a herd stays calm unless a unit there can both see and reach it. Being shot at, nearby rabbits and fleeing herds still make it run. On by default.

### New deer and goats
Off by default. When switched on, a new herd arrives when too few deer and goats are left on the map, also in the game modes where the base game brings none. You set the minimum number of animals (default 6) and how often this is checked (default every 120 seconds at normal speed). At most one herd comes per check, with the usual size of 9 to 12 deer or 4 to 5 goats. This has no use while another mod removes all animals.

### Chickens per player
Each player keeps this number of chickens (default 10, up to 100, or none at all), regardless of food, as long as they own a granary, bakery or mill. Surplus chickens are removed. Only applies while chicken hunting is enabled.

### Rabbits only on attack order
Melee units no longer attack rabbits on their own, so your soldiers do not wander off after them. After you order an attack on a rabbit, they also attack rabbits nearby until their next order, as ranged units already do. On by default.

## Tannery: meat from cows

A slider in the Tannery panel divides the three hides of a cow between meat and the Tannery's weapon or armour: far left for meat only, far right for weapon or armour only. Each Tannery is set on its own and starts with no meat. The slider works alongside the Tannery's normal production choice, so weapons or armour added by other mods are produced the same way.

Meat goes to the granary, after the Tanner has finished the other runs of that cow. You set the base amount per trip in the mod settings (default 5, up to 100); the production bonus applies here as well. The slider position is kept in saved games. You can switch the whole feature off in the mod settings.

## Requirements and settings

This mod requires the Script Extender and **APIShared 0.27.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games.

All players in a multiplayer game need this mod, and the host's settings apply to everyone. Settings can only be changed before or after a match.
