@echo off
setlocal EnableExtensions DisableDelayedExpansion
title Casualties Hub Uninstaller

rem This script is intentionally standalone. Place it beside a Casualties Hub
rem release folder, or use the "choose another folder" option below.
set "CH_DATA=%LOCALAPPDATA%\CasualtiesHub"
set "SCRIPT_FOLDER=%~dp0"
set "TARGET="
if exist "%SCRIPT_FOLDER%Casualties Hub.exe" set "TARGET=%SCRIPT_FOLDER%"
if exist "%SCRIPT_FOLDER%CasualtiesHub.exe" set "TARGET=%SCRIPT_FOLDER%"

call :KillHubProcesses

:Menu
cls
echo ==================================================
echo                 CASUALTIES HUB UNINSTALLER
echo ==================================================
echo.
echo Hub applications were asked to close before this menu opened.
echo.
echo  [1] Uninstall the Hub installation beside this CMD file
if defined TARGET echo      %TARGET%
if not defined TARGET echo      No Hub EXE was found beside this CMD file. You will choose a folder.
echo.
echo  [2] Choose another Casualties Hub folder
echo  [3] Search common locations for Casualties Hub files
echo  [4] Search a specific drive or folder (can take a long time)
echo  [Q] Quit
echo.
choice /C 1234Q /N /M "Choose an option"
if errorlevel 5 goto :End
if errorlevel 4 goto :SearchCustom
if errorlevel 3 goto :SearchCommon
if errorlevel 2 goto :PickFolder
if errorlevel 1 if defined TARGET goto :ConfirmTarget
if errorlevel 1 goto :PickFolder
goto :Menu

:PickFolder
set "TARGET="
for /f "usebackq delims=" %%I in (`powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; $dialog=New-Object System.Windows.Forms.FolderBrowserDialog; $dialog.Description='Select the Casualties Hub release folder to remove'; if($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($dialog.SelectedPath)}"`) do set "TARGET=%%I"
if not defined TARGET (
    echo.
    echo No folder was selected.
    pause
    goto :Menu
)
goto :ConfirmTarget

:ConfirmTarget
for %%I in ("%TARGET%") do set "TARGET=%%~fI"
if not exist "%TARGET%\" (
    echo.
    echo The selected folder does not exist:
    echo %TARGET%
    pause
    goto :Menu
)
call :ValidateTarget "%TARGET%"
if errorlevel 1 (
    echo.
    echo Refusing to remove a drive root or a protected broad folder.
    pause
    goto :Menu
)

cls
echo ==================================================
echo Selected launcher folder:
echo %TARGET%
echo.
echo [1] Remove this launcher folder ONLY
echo     Keeps settings, protected assets, logs, and downloads in:
echo     %CH_DATA%
echo.
echo [2] REMOVE EVERYTHING
echo     Removes the launcher folder AND all Casualties Hub user data,
echo     including protected assets, settings, logs, downloads, and caches.
echo.
echo [B] Back
echo.
choice /C 12B /N /M "Choose removal type"
if errorlevel 3 goto :Menu
if errorlevel 2 goto :RemoveEverything
if errorlevel 1 goto :RemoveLauncherOnly
goto :Menu

:RemoveLauncherOnly
call :FinalConfirm "launcher folder only"
if errorlevel 1 goto :Menu
call :KillHubProcesses
echo.
echo Removing launcher folder...
rmdir /S /Q "%TARGET%" 2>nul
if exist "%TARGET%\" (
    echo Could not completely remove the launcher folder.
    echo A Hub process, File Explorer window, or antivirus may still be locking a file.
) else (
    echo Launcher folder removed. Your Casualties Hub user data was kept.
)
goto :Finish

:RemoveEverything
call :FinalConfirm "ALL Casualties Hub files and user data"
if errorlevel 1 goto :Menu
call :KillHubProcesses
echo.
echo Removing launcher folder...
rmdir /S /Q "%TARGET%" 2>nul
echo Removing Casualties Hub user data...
if exist "%CH_DATA%\" rmdir /S /Q "%CH_DATA%" 2>nul
if exist "%TARGET%\" (
    echo Some launcher files could not be removed. Check for a locked file.
) else (
    echo Launcher folder removed.
)
if exist "%CH_DATA%\" (
    echo Some user data could not be removed. Check for a locked file.
) else (
    echo Casualties Hub user data removed.
)
goto :Finish

:SearchCommon
cls
echo Searching common locations. This can take a minute...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$roots=@($env:USERPROFILE+'\Desktop',$env:USERPROFILE+'\Downloads',$env:USERPROFILE+'\Documents',$env:LOCALAPPDATA,$env:ProgramFiles,${env:ProgramFiles(x86)}); $names=@('Casualties Hub.exe','CasualtiesHub.exe','Casualties Hub Developer Console.exe','Developer Console.exe','CH Uninstaller.cmd'); $found=foreach($root in $roots){if(Test-Path -LiteralPath $root){Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {$names -contains $_.Name} | ForEach-Object {$_.DirectoryName}}}; $found | Sort-Object -Unique"
echo.
echo Search complete. Copy one of the displayed folders and use option 2.
pause
goto :Menu

:SearchCustom
cls
echo Enter a drive or folder to search. Examples: C:\ or F:\SteamLibrary
set "SEARCH_ROOT="
set /P "SEARCH_ROOT=Search location: "
if not defined SEARCH_ROOT goto :Menu
if not exist "%SEARCH_ROOT%\" (
    echo That location does not exist.
    pause
    goto :Menu
)
echo.
echo Searching %SEARCH_ROOT% ... This may take a long time.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$root=$env:SEARCH_ROOT; $names=@('Casualties Hub.exe','CasualtiesHub.exe','Casualties Hub Developer Console.exe','Developer Console.exe','CH Uninstaller.cmd'); Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue | Where-Object {$names -contains $_.Name} | ForEach-Object {$_.DirectoryName} | Sort-Object -Unique"
echo.
echo Search complete. Copy one of the displayed folders and use option 2.
pause
goto :Menu

:KillHubProcesses
for %%P in ("Casualties Hub.exe" "CasualtiesHub.exe" "Casualties Hub Developer Console.exe" "Developer Console.exe") do taskkill /F /IM "%%~P" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$names=@('Casualties Hub.exe','CasualtiesHub.exe','Casualties Hub Developer Console.exe','Developer Console.exe'); Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {$names -contains $_.Name -or ($_.ExecutablePath -and $_.ExecutablePath -like '*Casualties Hub*')} | ForEach-Object {Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue}" >nul 2>&1
timeout /T 2 /NOBREAK >nul
exit /B 0

:ValidateTarget
set "CHECK_TARGET=%~f1"
if "%CHECK_TARGET:~-1%"=="\" set "CHECK_TARGET=%CHECK_TARGET:~0,-1%"
if /I "%CHECK_TARGET%"=="%SystemDrive%" exit /B 1
if /I "%CHECK_TARGET%"=="%USERPROFILE%" exit /B 1
if /I "%CHECK_TARGET%"=="%ProgramFiles%" exit /B 1
exit /B 0

:FinalConfirm
echo.
echo Type DELETE to confirm removal of %~1.
set "CONFIRM="
set /P "CONFIRM=Confirmation: "
if /I not "%CONFIRM%"=="DELETE" (
    echo Cancelled.
    exit /B 1
)
exit /B 0

:Finish
echo.
echo Press any key to close this uninstaller.
pause >nul
:End
endlocal
exit /B 0
