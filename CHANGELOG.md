# Changelog

## [2.0.1] - 2026-09-04

> Some dances have been removed due to the new project restructure and the fact they
> looked weird or broken, I'm very sorry for that. I know a lot of you liked some of
> them, and in the future, if I can, I will try to animate the removed dances.

### Changed

- Emotes now play on your own avatar. The mod used to spawn a second robot at your
  feet, animate that, and hide the real one. Your cosmetics, health bar and crown are
  on the body because they are never taken off it, and your head keeps moving with
  your voice while you dance.
- New emote wheel: hold the key, point with the mouse, release to dance. Release in the
  middle to cancel, scroll to turn the page, right click to pin a favourite.
- The emote key moved from P to F, and is now a setting rather than a line in
  `preferences.json`: rebind it under `Emote wheel / Key`, in the config file or in
  REPOConfig. A key you had already chosen is carried over on first launch.
- Emotes now travel as Photon events rather than RPCs, so players without the mod no
  longer get errors in their console when someone near them dances.
- Emotes fade in and out instead of snapping, and stop by themselves on death, a tumble
  or extraction.
- Players who join mid-dance now see it.

### Removed

- The emote clone, and everything that existed to maintain it.
- The `Debug` settings. They drove the rig probe, a tool for inspecting avatar bones
  that was only ever of use while building this version; it no longer ships.
- Ten dances, leaving 26. They all relied on something the REPO robot does not have:
  hands and forearms to take its weight, or a spine and knees that bend. The old system
  could fake them because it danced a separate, deformable copy of the robot; your real
  avatar is built from rigid parts with no elbow and no knee joint at all.
  - Bboy Hip Hop Move
  - Breakdance 1990
  - Breakdance Footwork 2
  - Breakdance Freeze Var 2
  - Breakdance Ready
  - Capoeira
  - Dancing Twerk
  - Flair
  - Head Spinning
  - Northern Soul Spin

### Renamed

Mixamo exports several variants of a dance under the same name, so they arrived as
`Hip Hop Dancing (1)` and `(2)`. They are now numbered plainly.

- `Breakdance Uprock Var 1` becomes `Breakdance Uprock`
- `Female Dance Pose` and `Female Dance Pose (1)` become `Female Dance Pose 1` and `2`
- `Hip Hop Dancing`, `(1)` and `(2)` become `Hip Hop Dancing 1`, `2` and `3`
- `Samba Dancing`, `(1)` and `(2)` become `Samba Dancing 1`, `2` and `3`
- `Thriller Part 3` becomes `Thriller`

### Note for multiplayer

Everyone should be on the same version. A 1.0.6 player and a 2.0.0 player will not see
each other's emotes at all, and two 2.0.0 players with different emote files will skip
the dances they do not share rather than play the wrong one.