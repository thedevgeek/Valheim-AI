Lucy - Valheim Companion 0.40.0
Stable production package

INSTALLATION
For a normal Steam launch, install the plugin and BepInEx files into the
Valheim game directory:
<Steam library>\steamapps\common\Valheim\

The BepInEx 5 loader must already be installed there. Extract the
ValheimCompanion folder from this archive into:
<Valheim game directory>\BepInEx\plugins\

Start Valheim from Steam. The BepInEx log should report Valheim Companion
0.40.0. If you also use r2modman, install the plugin in its profile separately;
the Steam game directory and r2modman profile load mods independently.

The archive contains the plugin DLL and this file. BepInEx and Valheim are
required and are not included.

CONTROLS
F6 - Defend you from nearby enemies targeting you or Lucy.
F7 - Gather nearby dropped items and harvestable plants into your inventory.
F8 - Stop the active mode.
F9 - Write companion status to the BepInEx log.
F10 - Summon or refocus Lucy.
F11 - Dismiss Lucy from the current world.

Hotkeys can be changed in BepInEx/config/com.valheimai.companion.cfg.
Summoning, dismissing, combat, and gathering require the host in multiplayer.

FEATURES
- Lucy uses Valheim's animated wolf creature for movement and combat.
- Fight mode engages nearby enemies threatening either you or Lucy.
- Lucy is immune to incoming damage and eligible bites receive +15 physical
  damage.
- Gather mode collects loose item drops and harvestable plants within 6 m.
- Lucy has a white coat, a right-eye patch, and a purple collar.
- Damage diagnostics record health before and after the first five allowed
  hits against each target in the BepInEx log.

The plugin targets Valheim single-player and Steam-hosted worlds. Dedicated
server and non-host control are not supported. Back up important worlds before
using mods that spawn persistent creatures.
