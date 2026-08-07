Casualties Hub - Linux Edition
v0.0.8-pre.6.1  ·  PRE-ALPHA TEST BUILD

===============================================================================
READ THIS FIRST - THIS BUILD CAN MODIFY YOUR MODS
===============================================================================

This build can install, enable, disable and DELETE mods in your BepInEx plugins
folder. Deleting is permanent and cannot be undone from inside the Hub.

>>> BACK UP YOUR BepInEx/plugins FOLDER BEFORE USING IT. <<<

    cp -r ~/.local/share/Steam/steamapps/common/"Casualties Unknown Demo"/BepInEx/plugins \
          ~/plugins-backup

    (adjust the path if your game is in a different Steam library - the Status
     page shows the exact path this Hub detected)

Nothing happens without you clicking a button, and deleting always asks first.
But this is pre-alpha software that has never run on real Linux hardware before
you, so assume it can get things wrong.

If you would rather not risk your install at all, the diagnostics report below
is still genuinely useful on its own and touches nothing.

The Windows version scans drive letters like C:\ and D:\ for Steam, and expects
folders to be named with exact capitalisation. Neither of those assumptions
holds on Linux, so this had to be rewritten. This build tests that rewrite.


===============================================================================
REQUIREMENTS
===============================================================================

  - 64-bit x86 Linux (x86-64 / amd64). ARM is not supported.
  - Steam, with Casualties Unknown installed.
  - Nothing else. The .NET runtime is bundled inside the binary.

Some minimal installs are missing libraries every GUI app needs. If the window
does not appear, install these:

  Debian / Ubuntu / Mint / Pop!_OS
      sudo apt install libice6 libsm6 libfontconfig1 xdg-utils

  Fedora
      sudo dnf install libICE libSM fontconfig xdg-utils

  Arch / Manjaro / SteamOS
      sudo pacman -S libice libsm fontconfig xdg-utils


===============================================================================
RUNNING IT
===============================================================================

Open a terminal in the folder you extracted this into, then:

    chmod +x casualties-hub
    ./casualties-hub

The chmod is only needed once.


===============================================================================
WHAT WOULD HELP MOST
===============================================================================

Run this and send back the output. It needs no window, so it works over SSH and
on a Steam Deck in desktop mode:

    ./casualties-hub --diagnostics > hub-report.txt 2>&1

Then attach hub-report.txt.

That output shows which Steam libraries were found, whether Casualties Unknown
was detected, and the exact capitalisation of your BepInEx folders. That last
detail is the single most useful thing in the file: BepInEx creates either
"plugins" or "Plugins" depending on how it was installed, and knowing which one
you actually have decides how the rest of this port gets written.

It contains your home directory path and your installed mod filenames. Nothing
else. No account details, no API keys.


===============================================================================
IF SOMETHING GOES WRONG
===============================================================================

Nothing happens when you double-click it
    Run it from a terminal instead. Some file managers will not launch a binary
    with no file extension, and running it from a terminal shows the error.

"Permission denied"
    You missed the chmod step above.

"cannot execute binary file"
    You are not on 64-bit x86. Send me the output of:  uname -m

The window opens but is blank, or the app is very slow
    Try forcing software rendering:
        LIBGL_ALWAYS_SOFTWARE=1 ./casualties-hub

It says my game was not found
    That is a genuinely useful result, not a waste of time. Send the
    --diagnostics output plus the folder your game is actually installed in.

Logs are written to:
    ~/.local/share/CasualtiesHub/Logs/

Check that every screen loads, without needing a window:
    ./casualties-hub --selftest


===============================================================================
WHAT WORKS, AND WHAT DOES NOT
===============================================================================

Working:
    Status page       game detection, Steam library list, environment report,
                      copy-diagnostics button, launch game through Steam
    Local Mods page   list installed mods, enable, disable, delete,
                      install from a .zip / .7z / .rar
    Skins page        lists CustomSprites slots, flags missing sprites, and
                      renders a preview with head/eye/facing controls
    Backups page      copy your whole plugins folder, restore it, delete old
                      copies. Taking a backup never touches your live mods.
    Settings page     set the game and downloads folders by hand, text size,
                      the four theme colours, and your Nexus API key

Installing a CustomSprites skin now works too: you are asked which st# slot it
should replace, and warned if that slot already has art in it.

>>> Before enabling, disabling or deleting anything, go to Backups and press
    "Back up now". It is one click and it copies everything. <<<

If detection does not find your game, Settings > Game folder > Browse lets you
point the Hub at it yourself. Please still send the diagnostics report if that
happens, since detection failing is exactly what I need to fix.

Not built yet:
    Skin preview rendering      Backups and protected files
    Nexus browsing/downloads    Modlist sharing
    Multiplayer server list     The Hub uninstaller

Automatic import is on: drop a mod archive into your downloads folder and the
Hub offers to install it once the download finishes. It waits for the file to
stop growing rather than trusting a file lock, because file locks on Linux are
advisory and would let a half-finished download through.

Known gaps specific to this build:
    - The skin preview draws the idle pose only; there is no animation.
    - Backups are stored in ~/.local/share/CasualtiesHub/Backups, not next to
      the binary, so they survive you moving or re-extracting the app.
    - There is no in-app uninstaller yet. To remove the Hub by hand, delete the
      folder you extracted and ~/.local/share/CasualtiesHub.

Please report anything that lists a mod incorrectly, especially if a mod shows
the wrong name or version, or if one is missing from the list entirely.
