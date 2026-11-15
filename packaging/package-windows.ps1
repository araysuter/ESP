$ErrorActionPreference = 'Stop'
$version = $env:ESP_VERSION
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid ESP_VERSION' }
New-Item -ItemType Directory -Force build/StandaloneWindows64, dist | Out-Null
tar -xzf windows-player.tar.gz -C build/StandaloneWindows64
if ($LASTEXITCODE -ne 0) { throw 'Player extraction failed' }
if (-not (Test-Path build/StandaloneWindows64/ESP.exe)) { throw 'Missing Windows executable' }
$compiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $compiler)) { throw 'The hosted Windows image must provide Inno Setup 6' }
& $compiler "/DAppVersion=$version" packaging/windows.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$installer = (Resolve-Path "dist/ESP-$version-Windows-Setup.exe").Path
$installDir = Join-Path $env:RUNNER_TEMP 'ESP-installed'
$process = Start-Process $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$installDir`"" -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Installer exited $($process.ExitCode)" }
python packaging/run-player-smoke.py "$installDir/ESP.exe" "$PWD/build/windows-smoke.log"
if ($LASTEXITCODE -ne 0) { throw 'Installed player smoke failed' }
Compress-Archive -Path build/StandaloneWindows64/* -DestinationPath "dist/ESP-$version-Windows.zip"
