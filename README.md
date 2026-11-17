# Projectile Landing Tracker

A Mount & Blade II: Bannerlord (v1.5.7) mod that shows where your shot will land: arrows, bolts, javelins
and thrown weapons, plus ballistae, catapults and trebuchets.

## Features

- **Landing marker** where the projectile is predicted to hit, with an optional flight-path line.
- **Color coding**, applied to the marker, the line and the game's own crosshair:

  | Color      | Meaning                                              |
  |------------|------------------------------------------------------|
  | White      | Terrain or a scene object                            |
  | Green      | Would hit an enemy                                   |
  | Red        | Would hit a friendly unit (friendly fire)            |
  | Light blue | Siege shot that would hit another siege engine       |

- **Stops at the first unit in the path**, on foot or mounted (riderless horses are ignored).
- **Movement-aware**: forward/backward speed of the player (or mount) is added to the projectile.
  Sideways momentum is not, because Bannerlord doesn't inherit it either.
- **Siege engines**: works while you operate a ballista, catapult or trebuchet (fire variants included).
  Rams and siege towers count as "other siege engines" for the light-blue highlight.
- **Real launch points**: after your first shot with a weapon, the prediction starts where the game actually
  spawns your arrow, bolt or thrown weapon (separately on foot and mounted). Ballistae start from the loaded
  bolt; catapults and trebuchets learn their release point from real shots, including the AI's. Nothing is
  saved: it's re-learned each time you start the game.
- **Spread ring** for siege engines (can be turned off): shows how far the game's random aiming error can scatter the
  shot around the marker.
- **Optional distance readout**, next to the marker and/or the crosshair.
- Hides itself while a menu (Esc, inventory, ...) is open.
- Every feature, and each weapon group (bows, crossbows, throwing weapons, siege engines), can be switched on or off in-game.

**F9** toggles the tracker during a mission. All other options live in
*Mod Options > Projectile Landing Tracker* (Mod Configuration Menu).

## Requirements

Install and enable these, above this mod in the launcher's load order:

1. [Bannerlord.Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006)
2. [Bannerlord.ButterLib](https://www.nexusmods.com/mountandblade2bannerlord/mods/2018)
3. [Bannerlord.UIExtenderEx](https://www.nexusmods.com/mountandblade2bannerlord/mods/2102)
4. [Mod Configuration Menu v5](https://www.nexusmods.com/mountandblade2bannerlord/mods/612)

## Building

Requires the .NET SDK and a Bannerlord install.

1. Point the build at the game: set the `BANNERLORD_GAME_DIR` environment variable, or edit `<GameFolder>`
   in `ProjectileLandingTracker.csproj`.
2. Run `dotnet build -c Release`. The first build needs internet to fetch the MCM reference package.

The build writes the DLL, `SubModule.xml` and `ModuleData` (translations) straight into
`<game>/Modules/ProjectileLandingTracker`. That folder's `bin/Win64_Shipping_Client` should contain only
`ProjectileLandingTracker.dll`; delete any MCM or Harmony DLLs that appear.

## Project layout

```
Module/ProjectileLandingTracker/SubModule.xml   Module manifest and dependencies
Module/ProjectileLandingTracker/ModuleData/     English string table for translations
src/SubModule.cs                                Entry point; attaches the tracker to missions
src/Tracking/                                   Mission behavior, trajectory simulation, player motion
src/Siege/                                      Siege engine discovery and launch parameters
src/Rendering/                                  Recoloring of the game's crosshair
src/Configuration/                              MCM settings and null-safe accessors
src/Interop/                                    Reflection helpers and menu detection
```

Parts of the game that aren't public API (the crosshair UI, siege weapon internals, screen layers) are
accessed through reflection. If a game update renames something, the affected feature turns itself off
instead of crashing the mission.

## Limitations

- Prediction uses the game's gravity, air drag and unit collision, but steps the flight in small time
  slices, so it can differ by a few centimetres from where the real projectile lands.
- Menu hiding is heuristic (it learns the normal number of UI layers); disable *Hide in menus* if the
  tracker vanishes during normal play.

## Support

If you enjoy the mod, you can support me on [Ko-fi](https://ko-fi.com/darkjeffrey).

## License

[MIT](LICENSE)
