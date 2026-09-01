# SomeEmotesREPO v2 - ImGogole

A mod for R.E.P.O. that lets players dance.

Version 2.0.0 changes how emotes are played. They now run on your own avatar instead of
on a hidden copy of it, so your cosmetics, health bar and crown stay with you while you
dance, and your head still moves when you talk.

The mod is open source and still in development. Pull requests are welcome.

## Installation

The simplest way is the [Thunderstore Mod Manager](https://www.overwolf.com/app/thunderstore-thunderstore_mod_manager),
which installs BepInEx and the mod together.

To install by hand, download the release and unzip it into your profile's plugin
folder:

```
%AppData%\Thunderstore Mod Manager\DataFolder\REPO\profiles\<profile>\BepInEx\plugins
```

## Playing an emote

Hold `E` to open the emote wheel, point at a dance with the mouse, and release to play
it. Release near the middle to cancel.

- Scroll to turn the page.
- Right click a dance to pin it as a favourite; favourites fill the first page. Right
  click it again to unpin it.
- Moving, jumping or grabbing something ends the emote.

The camera pulls back while you dance so you can see your own avatar. Scroll to change
how far.

## Multiplayer

Players without the mod see nothing and get no errors, so a public lobby is safe.

Players who do have it should be on the same version. Emotes are identified by name, so
two installs with different dances skip the ones they do not share instead of playing
the wrong one. A 1.x player and a 2.x player will not see each other's emotes at all.

Singleplayer is not supported.

## Configuration

In `BepInEx/config/ImGogole.SomeEmotesREPO.cfg`:

- `Wheel / Sensitivity` sets how far the mouse has to travel to reach the outer dances.
- `Debug / RigProbe` enables a development tool for inspecting the avatar rig. It is off
  by default and needs a restart.

The emote key and your favourites are stored in `preferences.json`, next to the mod.
