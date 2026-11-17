param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$src=Join-Path $root 'compatibility\src\Il2CppInterop'
$references=Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll'
if(!(Test-Path -LiteralPath $references)){throw 'Generate the local IL2CPP references for your installed game first.'}
New-Item -ItemType Directory -Force "$src\Il2CppInterop.Runtime\Libs" | Out-Null
Copy-Item -LiteralPath $references -Destination "$src\Il2CppInterop.Runtime\Libs\Il2Cppmscorlib.dll" -Force
dotnet build "$src\Il2CppInterop.HarmonySupport\Il2CppInterop.HarmonySupport.csproj" -t:Rebuild -c Release -p:GeneratePackageOnBuild=false -p:Version=1.5.1 -p:AssemblyVersion=1.5.1.0 -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Write-Output 'Built compatibility libraries under compatibility/src/Il2CppInterop/bin. No files were installed into the game.'
