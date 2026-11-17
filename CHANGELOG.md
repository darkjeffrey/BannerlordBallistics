# Changelog

## 1.1.1

- Updated for Bannerlord v1.5.7.
- The launcher no longer shows a dependency version warning: every required module now lists the version it was built against.

## 1.1.0

- Fix: hand-held shots now include the shooter's missile speed multiplier, as the game does, so the
  predicted range matches when a skill, perk or setting changes it.
- Gravity is now read from the game (MBGlobals.Gravity, 9.806 m/s²) instead of a hard-coded 9.8.
- Units are now detected with the game's own agent raycast against their collision shapes, instead of
  approximated cylinders, so shots that clip a shoulder or pass under a horse's belly are predicted
  correctly.
- Launch points: the prediction now starts where the game really spawns the missile. Hand-held weapons learn
  the spawn point from your own shots (on foot and mounted separately); ballistae use the loaded bolt's
  position; catapults and trebuchets learn their release point from real shots, per release angle. Before
  the first shot, the old start point is used. Learned points are kept in memory only.
- Siege shots now include the engine's own velocity, as the game does (zero for engines on land).
- New "Show spread ring" option (Siege engines, on by default): a ring around the marker showing where the
  game's random aiming error can put the shot (0.5 degrees for ballistae, 1 for trebuchets, 1.5 for
  catapults, 2.5 for multi-shot fire loads).
- "Show what stopped the shot" now also prints, after each of your shots, how far the real launch point and
  speed were from the prediction.

## 1.0.2

- Fix: bows, crossbows and thrown weapons were predicted to land too far, by up to 15-20% on long
  shots, because the prediction ignored air drag for hand-held weapons. It now uses the same drag the
  game applies to the arrow, bolt or thrown weapon (thanks to Inviop for the report and analysis).
- The mod's text (options page and the F9 message) can now be translated: the English string table is
  in ModuleData/Languages/std_ProjectileLandingTracker.xml.

## 1.0.1

- New "Weapons" options: turn the prediction on or off separately for bows, crossbows and throwing
  weapons. The siege engine options now sit right after them.
- Fix: the prediction no longer stops at invisible or non-missile collision shapes (for example the low
  collision of a wooden structure), which made it report a landing spot where the arrow would pass.
  The scene raycast now ignores the same body types the game ignores for missiles.
- Fix: hits on siege engines are now detected from the object actually hit (previously this
  always fell back to "landing point near another engine").
- Fix: bows, crossbows and thrown weapons now turn light blue on a siege engine too (for example a
  stone thrown at a battering ram). Before, only shots from a siege engine you operated did.
- Fix: siege engine shots landed a few metres short of the marker. The prediction now applies the
  same air drag the game uses for the engine's missile.
- New "Debug" option: shows what stopped the prediction (terrain, agent, or object name and body flags).

## 1.0.0

- Landing prediction for hand-held ranged weapons and siege engines.
- Color-coded marker and crosshair: ground, enemy, friendly fire, other siege engine.
- Optional trace line and distance readout (at marker and/or crosshair).
- Forward-momentum aware; sideways momentum ignored, as in the game.
- In-game options through Mod Configuration Menu; F9 toggle.
- Hides while menus are open.
