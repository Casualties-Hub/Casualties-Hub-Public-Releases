Casualties Hub on Linux
===============================================================================

These notes cover what is different about running the Hub on Linux. Everything
else works the same as on Windows.


REQUIREMENTS
-------------------------------------------------------------------------------

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


RUNNING IT
-------------------------------------------------------------------------------

Open a terminal in the folder you extracted this into, then:

    chmod +x casualties-hub
    ./casualties-hub

The chmod is only needed once. To add the Hub to your application menu, run
./install-desktop-entry.sh from the same folder. It only writes inside your
home directory.


WHERE THINGS LIVE
-------------------------------------------------------------------------------

Settings, logs, backups and the Nexus API key are kept in:
    ~/.local/share/CasualtiesHub/

To remove the Hub, delete the executable and that directory. If you ran
install-desktop-entry.sh, it printed the one file to delete for the menu entry.


IF SOMETHING GOES WRONG
-------------------------------------------------------------------------------

Nothing happens when you double-click it
    Run it from a terminal instead. Some file managers will not launch a binary
    with no file extension, and running it from a terminal shows the error.

"Permission denied"
    You missed the chmod step above.

"cannot execute binary file"
    You are not on 64-bit x86. Check with:  uname -m

The window opens but is blank, or the app is very slow
    Try forcing software rendering:
        LIBGL_ALWAYS_SOFTWARE=1 ./casualties-hub

The game was not found
    Settings > Game folder > Browse lets you point the Hub at it yourself.
    Steam is found through its own library index, so Flatpak Steam and extra
    libraries on other drives are supported.

For a bug report, this writes a diagnostics summary without opening a window:

    ./casualties-hub --diagnostics > hub-report.txt 2>&1

Read it before sharing. It contains your username, your operating system and
.NET versions, the folders the Hub uses, your Steam library paths, the game's
install path and up to fifteen installed mod filenames. It never includes your
Nexus API key, your settings or any account details.
