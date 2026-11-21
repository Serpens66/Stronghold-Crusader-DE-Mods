# ImprovedHunters

Makes Hunters more useful and gives Tanneries a second purpose. Hunters choose their prey sensibly, also hunt rabbits, camels and your own chickens, collect free carcasses and find their way around obstacles. Tanneries can turn cows into meat. Everything can be set in the mod settings.

## Hunters

### What they hunt and how much meat it gives
Hunters hunt deer, goats, rabbits, camels and your own chickens. In the mod settings you can switch each animal off and set how much meat it gives.

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

### AI Hunter's huts
The AI can otherwise build Hunter's huts even on maps without prey. Three separate options, all on by default, help it use these buildings sensibly:

- **Check planned AI Hunter's huts for prey** lets the AI skip huts from its castle plan when there is nothing to hunt. One hut is allowed for its own chickens; additional huts require other prey.
- **AI builds a Hunter's hut when useful** lets an AI without one build a hut near its first granary, even if its castle plan contains none. It needs a keep, a granary, a suitable site and the normal building materials.
- **AI removes Hunter's huts without prey** removes unneeded huts after their current work and deliveries have finished. With only chickens, the AI keeps the hut closest to its first granary. Other prey allows it to keep additional huts.

A replenishing supply of the AI's own chickens can support its first hut even while no chickens are currently alive. This requires chicken hunting, a positive chicken population setting, active replenishment and one of its own granaries, mills or bakeries. This allowance does not apply in the map editor, where automatic chicken replenishment is inactive. Existing huntable animals still count there.

These decisions use the enabled animal types and free carcasses across the map. They do not check the distance or route to each animal, so available prey does not guarantee that a Hunter can reach it. The first automatic build or removal check runs after 200 simulation ticks, then every 4800 ticks: about 5 seconds and 2 minutes at speed 40. Planned hut construction is checked when the AI attempts it.

Automatic building searches within 32 tiles of the first granary, at four-tile intervals, with at most 256 candidate sites and 64 placement attempts per check. It builds at most one hut per AI and only when that AI has none. Space reserved by its active castle plan, including the footprints of future buildings, stays free. If that plan reserves every search site, this AI stops searching for the rest of the match. Other obstacles or missing materials can change, so those cases are checked again later. Loading a save starts a fresh check. The three options affect AI players only and can be switched independently before a game.

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

Meat goes to the granary, after the Tanner has finished the other runs of that cow. You set the meat per trip in the mod settings. The slider position is kept in saved games. You can switch the whole feature off in the mod settings.

## Requirements and settings

This mod requires the Script Extender and **APIShared 0.60.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games.

All players in a multiplayer game need this mod, and the host's settings apply to everyone. Settings can only be changed before or after a match.
