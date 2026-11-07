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
In the base game, a herd runs from any unit in its area, even one behind a wall. With this option a herd stays calm unless a unit there can both see and reach it. Being shot at and nearby lions, hyenas or crocodiles still make it run. On by default.

### Herds do not scare each other
In the base game a fleeing herd makes all herds near it run, which on full maps never ends and keeps them from growing. With this option, which is on by default, a herd only runs from units, predators and being shot at.

### Extinct herds return
Off by default. Each deer or goat herd is followed on its own. When one has died out completely, two animals return to its start place after the time you set, and the herd grows from there by itself. About 10 minutes at normal speed is a good start. A map without deer and goats gets none, unless you use herds at the players' castles. This works in the game modes where the base game brings no new animals, not in the map editor, and has no use while another mod removes all animals.

### Area of deer and goats
No limit by default. You can set a size, for example 100 by 100 tiles. Deer and goat herds then do not go far from their start place when wandering or fleeing, which keeps Hunters' walks short and makes these animals easier to hunt. Single animals can briefly cross the border when startled. Other animals are not affected. This works in all game modes except the map editor, where animals do not move.

### Herds at the players' castles
Off by default. At the start of a new game every player, AI included, gets a herd about 55 tiles from their castle, just outside the area an AI castle layout covers, either only on maps without deer and goats or on every map. All herds have the same size: 9 deer, or 4 goats if deer cannot be hunted. The Hunter's hut stays available when this is on. These herds return and keep to their area like any other.

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
