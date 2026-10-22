# Changelog

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
