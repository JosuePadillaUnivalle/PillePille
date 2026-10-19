$ErrorActionPreference = 'Stop'
$unityPath = 'C:/Program Files/Unity/Hub/Editor/2020.3.49f1/Editor/Unity.exe'
if (-not (Test-Path -LiteralPath $unityPath)) { throw 'No se encontro Unity 2020.3.49f1. Edita unityPath en este script o compila desde el editor.' }
$projectPath = $PSScriptRoot
$buildFolder = [System.IO.Path]::GetFullPath((Join-Path $projectPath '../PillePille_Windows'))
$exePath = Join-Path $buildFolder 'PillePille.exe'
$evidenceFolder = Join-Path $projectPath 'Pruebas'
New-Item -ItemType Directory -Path $evidenceFolder -Force | Out-Null
$buildLog = Join-Path $evidenceFolder 'compilacion_actual.log'
$buildArguments = '-batchmode -nographics -quit -projectPath "' + $projectPath + '" -executeMethod ProjectBuilder.Build -pilleBuildPath "' + $exePath + '" -logFile "' + $buildLog + '"'
Write-Host 'Compilando con Unity 2020. Cierra antes el editor si tiene este proyecto abierto.'
$buildProcess = Start-Process -FilePath $unityPath -ArgumentList $buildArguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) { throw "Unity no pudo compilar. Comprueba la licencia en Unity Hub y revisa $buildLog" }
$testLog = Join-Path $evidenceFolder 'integracion_actual.log'
$testArguments = '-batchmode -nographics -pilleTest -logFile "' + $testLog + '"'
Write-Host 'Ejecutando pruebas dentro del juego...'
$testProcess = Start-Process -FilePath $exePath -ArgumentList $testArguments -WindowStyle Hidden -PassThru -Wait
$testReport = Join-Path $buildFolder 'verificacion.txt'
if (Test-Path -LiteralPath $testReport) { Copy-Item -LiteralPath $testReport -Destination (Join-Path $evidenceFolder 'integracion_actual.txt') -Force; Get-Content -LiteralPath $testReport }
if ($testProcess.ExitCode -ne 0) { throw "Fallaron pruebas. Revisa $testLog" }
$botLog = Join-Path $evidenceFolder 'bots_actual.log'
$botArguments = '-batchmode -nographics -pilleBotTest -logFile "' + $botLog + '"'
Write-Host 'Ejecutando pruebas de navegacion, persecucion y rescate de bots...'
$botProcess = Start-Process -FilePath $exePath -ArgumentList $botArguments -WindowStyle Hidden -PassThru -Wait
$botReport = Join-Path $buildFolder 'verificacion_bots.txt'
if (Test-Path -LiteralPath $botReport) { Copy-Item -LiteralPath $botReport -Destination (Join-Path $evidenceFolder 'bots_actual.txt') -Force; Get-Content -LiteralPath $botReport }
if ($botProcess.ExitCode -ne 0) { throw "Fallaron pruebas de bots. Revisa $botLog" }
Write-Host "Compilacion y pruebas correctas. Juego: $exePath"
