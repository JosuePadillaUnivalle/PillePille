$ErrorActionPreference = 'Stop'
$unityData = 'C:/Program Files/Unity/Hub/Editor/2020.3.49f1/Editor/Data'
$projectScripts = (Join-Path $PSScriptRoot '../../Assets/Scripts')
$scratchDir = $PSScriptRoot
$compiler = Join-Path $unityData 'Tools/Roslyn/csc.exe'
$monoRuntime = Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe'
$outputExe = Join-Path $scratchDir 'RulesHarness.exe'
$frameworkRefs = Join-Path $unityData 'MonoBleedingEdge/lib/mono/4.7-api'
& $compiler /nologo /nostdlib /target:exe "/reference:$frameworkRefs/mscorlib.dll" "/reference:$frameworkRefs/System.dll" "/reference:$frameworkRefs/System.Core.dll" "/out:$outputExe" (Join-Path $scratchDir 'RulesHarness.cs') (Join-Path $projectScripts 'Protocol.cs') (Join-Path $projectScripts 'MatchSimulation.cs')
if ($LASTEXITCODE -ne 0) { throw 'Rule harness compilation failed.' }
& $monoRuntime $outputExe | Tee-Object -FilePath (Join-Path $scratchDir 'rules_results.txt')
if ($LASTEXITCODE -ne 0) { throw 'One or more rule tests failed.' }

