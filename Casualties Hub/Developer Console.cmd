@echo off
setlocal EnableExtensions
title Casualties Hub Developer Console

:menu
cls
echo =====================================================
echo             Casualties Hub Developer Console
echo =====================================================
echo This CMD control panel sends one-time commands to a
echo running Casualties Hub. It never edits game files itself.
echo.
echo  1  Test: Cannot find Casualties Unknown
echo  2  Test: Missing BepInEx Plugins folder
echo  3  Test: Metadata request failure
echo  4  Test: Mod import failure
echo  5  Create a test crash report (does not crash the Hub)
echo.
echo  6  Reload Nexus Dashboard and metadata
echo  7  Reload Local Mods
echo  8  Open Settings in the Hub
echo  9  Create a 10-minute diagnostic log
echo 10  Test: Request Supabase status now
echo 11  Test: Simulate Supabase one-hour rate limit
echo 12  Check the eligible GitHub update feed
echo.
echo  L  Show latest Hub log
echo  O  Open Hub log folder
echo  Q  Quit
echo.
set /p option=Select an option: 
if /i "%option%"=="1" call :send MissingGameLocation
if /i "%option%"=="2" call :send MissingPluginsFolder
if /i "%option%"=="3" call :send MetadataRequestFailed
if /i "%option%"=="4" call :send ImportFailed
if /i "%option%"=="5" call :send CreateCrashReport
if /i "%option%"=="6" call :send ReloadDashboard
if /i "%option%"=="7" call :send ReloadLocalMods
if /i "%option%"=="8" call :send OpenSettings
if /i "%option%"=="9" call :send CreateDiagnosticLog
if /i "%option%"=="10" call :send CheckSupabaseNow
if /i "%option%"=="11" call :send SimulateSupabaseRateLimit
if /i "%option%"=="12" call :send CheckUpdateFeed
if /i "%option%"=="L" call :showlog
if /i "%option%"=="O" start "Casualties Hub Logs" "%LOCALAPPDATA%\CasualtiesHub\Logs"
if /i "%option%"=="Q" exit /b 0
pause
goto menu

:send
set "commandName=%~1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$data=Join-Path $env:LOCALAPPDATA 'CasualtiesHub'; $commandPath=Join-Path $data 'DeveloperCommand.json'; $responsePath=Join-Path $data 'DeveloperCommandResponse.json'; if(-not (Get-Process -Name 'Casualties Hub' -ErrorAction SilentlyContinue)){Write-Host 'Casualties Hub is not running. Start the Hub first.' -ForegroundColor Yellow; exit 2}; New-Item -ItemType Directory -Path $data -Force ^| Out-Null; $id=[guid]::NewGuid().ToString('N'); $body=@{RequestId=$id;Command='%commandName%';RequestedUtc=[DateTime]::UtcNow} ^| ConvertTo-Json -Compress; Remove-Item -LiteralPath $responsePath -Force -ErrorAction SilentlyContinue; $temp=$commandPath+'.tmp'; Set-Content -LiteralPath $temp -Value $body -NoNewline; Move-Item -LiteralPath $temp -Destination $commandPath -Force; Write-Host 'Command sent. Waiting for Hub acknowledgement...'; $until=[DateTime]::UtcNow.AddSeconds(4); while([DateTime]::UtcNow -lt $until){Start-Sleep -Milliseconds 150; if(Test-Path -LiteralPath $responsePath){try{$reply=Get-Content -LiteralPath $responsePath -Raw ^| ConvertFrom-Json; if($reply.RequestId -eq $id){Write-Host ('Hub confirmed: '+$reply.Message) -ForegroundColor Green; exit 0}}catch{}}}; Write-Host 'No acknowledgement. Make sure the running Hub is v0.0.3 or newer.' -ForegroundColor Red; exit 1"
exit /b

:showlog
powershell -NoProfile -Command "$logs=Join-Path $env:LOCALAPPDATA 'CasualtiesHub\Logs'; $latest=Get-ChildItem -LiteralPath $logs -Filter 'Log *.log' -ErrorAction SilentlyContinue ^| Sort-Object LastWriteTimeUtc -Descending ^| Select-Object -First 1; if($null -eq $latest){Write-Host 'No Hub log exists yet.'}else{Write-Host ('--- '+$latest.FullName+' ---'); Get-Content -LiteralPath $latest.FullName -Tail 120}"
exit /b
