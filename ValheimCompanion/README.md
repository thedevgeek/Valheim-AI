# Lucy - Valheim Companion

## Production release: 0.40.0

Version 0.40.0 is the stable production package. Combat was verified against Neck, Boar, and other enemies using BepInEx health-before/after diagnostics; the user confirmed the final appearance and behavior. The purple collar is wider and brighter than the previous build. It is installed in the Steam game directory for normal Steam launches; the r2modman profile remains separately available. For installation, controls, and supported modes, see [RELEASE-README.txt](./RELEASE-README.txt).

An in-game companion mod for Valheim. The supported target is single-player and Steam-hosted worlds. The companion is controlled by its owner.

## Current stage

**Version 0.40.0 (stable production release).** Lucy is verified to defend her owner and herself, kill enemies including Neck and Boar, and deal +15 physical damage with eligible bites. She is immune to damage, gathers nearby drops and harvestable plants, and has her white coat, eye patch, and purple collar. Supports single-player and Steam-hosted worlds; dedicated-server and non-host control are not supported.

## Hotkey smoke test

The plugin does not register or patch console commands. It adds configurable BepInEx shortcuts under `BepInEx/config/com.valheimai.companion.cfg`:

- F6 selects defend mode: the host-owned companion targets a nearby enemy while its AI is targeting the player or Lucy. The search radius is 24 meters.
- F7 selects gather mode: collect nearby dropped items and harvestable plants within 6 meters of the player. It does not chop trees or mine ore.
- F8 stops the selected mode.
- F9 reports the selected mode to the BepInEx log.
- F10 summons or refocuses Lucy.
- F11 dismisses the tagged companion from the current world.

Summoning, dismissing, defending, and gathering change in-world behavior. In multiplayer, these actions require the Steam-hosting player. Lucy is a persistent Valheim creature saved with the world; the underlying networked creature remains a vanilla wolf. Her original wolf animation is preserved. Fight mode is deliberately limited to nearby enemies targeting the player or Lucy. Gather mode collects loose drops and harvestable plants only; it will not damage trees or ore nodes. Modes are session-local and reset when the game restarts. Change a hotkey in the config file if it conflicts with another game or mod binding.

Versions 0.25.0 and later require the companion mod on the authoritative host for damage immunity and bite rules to apply. Install the same version on other players' clients for consistent local behavior. Validate immunity against both attacks and environmental damage in the test world before using Lucy in a long-term world.

## Prerequisites

- Valheim installed through Steam. The current development target is `M:\Steam\steamapps\common\Valheim`, game version `1.0.20 (n-41)`.
- The Valheim BepInEx 5 pack installed and run once so its folders are created.
- .NET SDK 6 or newer and the C# extension for VS Code.

The BepInEx pack and .NET 6 SDK are installed. BepInEx is isolated in the r2modman `valheim` profile at `C:\Users\watso\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\valheim`; the regular Steam game folder has not been modified.

## Safe test setup

1. In r2modman, select the `valheim` profile and use **Start modded** to launch Valheim.
2. Create and use a separate world for testing. Do not load your intended play world during initial testing; keep a backup before testing any later version that changes world state.
3. If you select a different mod-manager profile later, use **Browse profile folder** to find its `BepInEx` directory.
4. Verify the .NET SDK with `dotnet --list-sdks`.

## Build

From PowerShell in this folder, build and deploy directly to the installed r2modman profile:

```powershell
dotnet build .\ValheimCompanion.csproj `
  -p:ValheimDir="M:\Steam\steamapps\common\Valheim" `
  -p:BepInExDir="C:\Users\watso\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\valheim\BepInEx" `
  -p:DeployToGame=true
```

To build without deployment, omit `-p:DeployToGame=true`. Deployment is opt-in. Launch through the `valheim` profile and use the test world, not the world intended for regular play.

## Multiplayer expectations

The first multiplayer target is a Steam-hosted world. A mod that creates a custom companion must keep its world actions synchronized by the host. Friends may need the same mod and version to see custom content consistently; we will test this rather than assume clients without the mod can display it correctly. Commands will be owner-only.

Dedicated-server support is a separate validation step and is not promised by this initial scaffold.
