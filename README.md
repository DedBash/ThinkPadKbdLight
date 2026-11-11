# ThinkPadKbdLight

Tray tool that puts the ThinkPad keyboard backlight back to your chosen level
after boot, resume, lid open, display on and unlock. On Windows the firmware
tends to switch it off in all of those cases.

Tested on a T14 Gen 1 (20UE), Windows 11, levels 0-2. No admin rights needed.

## How it works

The backlight is driven by `Keyboard_Core.dll` from Lenovo Vantage, found under
`C:\ProgramData\Lenovo\Vantage\Addins\ThinkKeyboardAddin\<version>\`. The tool
loads it via reflection and only uses get, set and max level. Vantage itself
doesn't have to run, but the DLL and the Lenovo PM driver (`IBMPMDRV`) have to
be there. The DLL is 32 bit, so the exe is built as x86.

After a resume the EC isn't always ready, so a restore retries up to 5 times
over ~30 s. A level picked with Fn+Space is adopted as the new target level.
Turning it off with Fn+Space is not, so it comes back on the next resume.
Use "Off" in the tray menu if you want it off permanently.

## Build

```
build.cmd
```

Uses the `csc.exe` that ships with .NET Framework 4, no SDK needed (C# 5 only).

CI is set up for both GitHub (`windows-latest`, runs `build.cmd`) and Forgejo
(Linux runner, `mcs` from Mono). Every push builds the exe and attaches it as an
artifact. Pushing a tag like `v1.0` also creates a release.

## Usage

Run `ThinkPadKbdLight.exe`. The tray icon shows the current level, left click
cycles through the levels, right click opens the menu.

```
ThinkPadKbdLight.exe --get          print current level
ThinkPadKbdLight.exe --set <0..2>   set level
ThinkPadKbdLight.exe --on | --off
```

## Config

`%APPDATA%\ThinkPadKbdLight\settings.ini`, log in `log.txt` next to it.

| Key | Default | |
|---|---|---|
| `Level` | `2` | target level, 0 = off |
| `RestoreOnWake` | `1` | restore on start, resume, lid open, display on, unlock |
| `Enforce` | `0` | re-apply every 2 s, also against Fn+Space |
| `AdoptFnSpace` | `1` | adopt levels picked with Fn+Space |
| `DllPath` | empty | path to `Keyboard_Core.dll`, empty = auto-detect |
| `Language` | `auto` | `auto` follows the Windows display language, or a file name from `lang/` |

Lookup order for the DLL: `DllPath`, next to the exe, then the Lenovo folders
(newest version wins). Without Vantage, copy `Keyboard_Core.dll`, `mfc140u.dll`,
`msvcp140.dll` and `vcruntime140.dll` next to the exe.

## Translations

UI texts live in `lang/<code>.ini`, one file per language (English, German and
Swedish so far). The log is always English.

To add a language, copy `lang/en.ini` to e.g. `lang/fr.ini`, set `name=` to the
native language name and translate the values. `\n` is a line break, `{0}` a
placeholder. Missing keys fall back to English.

`build.cmd` embeds every file in `lang/` into the exe. A `lang\` folder next to
the exe is read on top of that, so a new file there works without a rebuild.

## Caveats

- It sits on a proprietary DLL. If Lenovo changes the interface, this breaks.
