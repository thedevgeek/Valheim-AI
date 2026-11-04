# Lucy - Valheim Companion

## Production release: 0.40.0

Version 0.40.0 is the stable production package. Combat was verified against Neck, Boar, and other enemies using BepInEx health-before/after diagnostics; the user confirmed the final appearance and behavior. The purple collar is wider and brighter than the previous build. It is installed in the Steam game directory for normal Steam launches; the r2modman profile remains separately available. For installation, controls, and supported modes, see [RELEASE-README.txt](./RELEASE-README.txt).

An in-game companion mod for Valheim. The supported target is single-player and Steam-hosted worlds. The companion is controlled by its owner.

## Current stage

Versions 0.2.0, 0.2.2, and 0.2.3 caused duplicate-command exceptions and UI issues at the main menu; their DLLs are disabled in the r2modman profile and retained as backups. Version 0.3.0 removed Harmony patches and passed the main-menu and hotkey tests. Version 0.4.0 summon, follow/refocus, and dismiss were confirmed in the separate test world. Version 0.5.0 defend mode was tested successfully: the wolf attacked Greydwarfs and resumed following after F8. Version 0.6.0's host-side collection of nearby dropped items and harvestable plants was tested successfully in-game. Version 0.7.0 briefly added resource-node damage; version 0.8.0 removed it. Version 0.9.0's procedural model was judged too cartoony with poor movement. Version 0.10.0 restored Valheim's animated wolf model and gait. Version 0.11.0 was too subtle, while version 0.12.0 looked too bright. Version 0.13.0 reduces coat brightness and softens shadows; version 0.14.0 added rig/material diagnostics; version 0.15.0 tested leg scaling/UV diagnostics; versions 0.16.0-0.18.0 exposed errors in the face-marking placement; version 0.19.0 replaces the broad texture mark with a single-side facial overlay, slightly lengthens the torso, and modestly shortens the legs. Version 0.20.0 fixes summon prefab lookup by preferring Valheim's canonical `Wolf` prefab and checking known alternate names only if it is unavailable. Version 0.21.0 increased torso length and tried a compact manually positioned eye patch. Version 0.22.0 attempted to select face triangles near the right eye and ear, but the in-game log showed no triangles were selected. Version 0.23.0 darkens the patch, broadens the face-triangle search while keeping it limited to the eye/ear side, modestly shortens and widens the head for a more corgi-like profile, and sets the character display name to Lucy. Version 0.24.0 scales Lucy's entire companion root uniformly to 0.80x her original size. Version 0.25.0 makes Lucy immune to damage and adds 15 physical damage to eligible bites, but testing showed that her AI could report a target without attacking. Version 0.26.0 directly replaces the wolf AI's private target state and updates its last-known target position. Version 0.27.0 temporarily clears the wolf's `m_aiCannotTargetOthers` restriction during a valid fight engagement and restores its original value outside combat; face-patch selection now compares baked mesh triangles and eye/ear bones in world space. Version 0.28.0 adds a purple collar band to the animated neck bone. Version 0.29.0 reasserts Lucy's selected fight target after the wolf AI update and expands F9 status with current target diagnostics. Version 0.30.0 restores the selected threat after Valheim's target-selection step. Version 0.31.0 allows bites against the active defended target if its AI temporarily switches away from the owner, with allow/block diagnostics. Version 0.32.0 retains the valid engaged threat while it is alive and within the defense radius; Lucy's attacks successfully damaged and killed enemies in the test world. Version 0.33.0 creates a prominent eye patch as a separate surface renderer and enlarged the collar, but the user's screenshot showed that both features still needed visual refinement. Version 0.34.0 paints the eye patch directly onto Lucy's private skin-texture copy using the known eye/ear UV region, avoiding oversized/skewed triangles, and restores the smaller neck collar dimensions to reduce the floating appearance.

Version 0.34.0's final appearance implementation keeps the eye patch as a one-sided, bone-weighted mesh overlay; oversized triangles at the UV boundary are filtered out. Version 0.35.0 restricted the patch to right-side face bones, but the game log showed the Standard shader was unavailable, so no collar was created. Version 0.36.0 removes the skinned mesh overlay in favor of a small patch attached directly to the right-eye bone, and uses Valheim's creature shader plus a solid texture for the purple collar. Version 0.37.0 corrects eye-patch triangle winding, anchors it using head-space eye coordinates, compensates for animated-bone scale, and centers a wider collar along Lucy's neck toward her head. Version 0.38.0 allows Fight mode to engage and damage nearby enemies targeting either the player or Lucy, fixing blocked bites when a monster turns to attack Lucy. Version 0.39.0 logs the health before and after each of Lucy's first five allowed hits per target, along with physical damage values, to verify whether Valheim applies damage. Version 0.40.0 widens and slightly enlarges the collar and brightens its purple tint/emission for better visibility.

Summoning creates a persistent vanilla wolf for networking, taming, following, and combat AI. Its original animation remains enabled. Version 0.13.0 reduces the coat material's neutral RGB multiplier to 0.82 and adds a small neutral emission fill where supported. Version 0.14.0 logs the model's bones and material. Version 0.19.0 keeps the verified paw scale, trims limb length slightly from 0.58 to 0.55, and increases the spacing of the torso/neck chain. Version 0.20.0 resolves the spawn prefab by trying `Wolf` first, then `Wolf_Tamed` and `Lucy` as explicit fallbacks. Version 0.21.0 sets the torso/neck chain to 1.10x. Version 0.22.0 increases that chain spacing to 1.12x and attempts a single-side surface patch. Version 0.23.0 uses a darker patch and searches a slightly wider face area for skinned triangles. It also scales the head to 0.90x length and 1.12x width, using the existing head bone so the wolf's native animation remains active. Version 0.24.0 uniformly scales the companion to 0.80x. Version 0.25.0 patches Valheim character damage: Lucy ignores all incoming `Character.Damage` calls, and outgoing Lucy hits are allowed only when the target is an enemy actively targeting her owner. Those hits gain +15 damage in the dominant physical damage type before Valheim applies damage modifiers. Version 0.26.0 replaces the AI's current target directly and updates its last-known target position. Version 0.27.0 temporarily disables `m_aiCannotTargetOthers` while Lucy has a valid fight target and restores its prior value when fighting ends; patch-triangle search uses world-space coordinates. Version 0.28.0 adds a solid purple collar band to the neck bone, so it follows Lucy's existing animation and scaling. Face/body speckles remain on a private copy of the wolf skin texture.

Dismiss removes only the tagged companion through Valheim's network scene. Fight mode directs the wolf to attack a nearby enemy targeting the player or Lucy; the host-owned companion is required, and the search radius is 24 meters. Gather mode uses Valheim's normal pickup interactions for dropped items and harvestable plants within 6 meters of the player, placing items in the player's inventory; items that cannot be picked up remain in the world. It does not damage trees, ore, or other resource nodes.

Agreed initial behavior:

- `fight`: defend the player by attacking enemies that threaten or attack them.
- `gather`: collect nearby dropped items and harvestable plants; do not chop trees or mine ore.
- Fight and gather remain active until `stop` is selected or the game restarts.
- Gathered resources should be added directly to the player's inventory; leave them in the world when the inventory cannot accept them.
- Lucy uses Valheim's vanilla wolf creature for networking, animation, and combat.

Release validation and scope:

1. Defend mode validated in the separate test world: a nearby enemy targeted the player, the wolf attacked, then resumed following after F8.
2. Version 0.6.0 dropped-item and harvestable-plant gathering validated successfully in the separate test world.
3. Version 0.40.0 verified: Lucy attacks enemies targeting either her owner or herself, is immune to incoming damage, deals +15 physical damage with eligible bites, and displays the right-eye patch and purple collar. BepInEx logs confirmed health reductions and kills, including against a Neck and Boar. The log reports health before and after the first five allowed hits per target.
4. Dedicated-server control and non-host client control are not supported. Steam-hosted multiplayer behavior and appearance synchronization have not been independently validated.

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
