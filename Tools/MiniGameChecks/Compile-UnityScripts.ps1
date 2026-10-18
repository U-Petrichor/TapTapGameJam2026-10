param([string]$UnityManagedDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $UnityManagedDirectory) {
    [xml]$unityProject = Get-Content -LiteralPath (Join-Path $projectRoot 'Assembly-CSharp.csproj')
    $core = $unityProject.Project.ItemGroup.Reference | Where-Object Include -eq 'UnityEngine.CoreModule' | Select-Object -First 1
    $UnityManagedDirectory = Split-Path $core.HintPath
}
if (-not (Test-Path -LiteralPath $UnityManagedDirectory)) { throw 'Unity managed DLL directory not found; regenerate Unity project files or pass -UnityManagedDirectory.' }
$checkDirectory = Join-Path $projectRoot 'Temp/MiniGameCompileChecks'
New-Item -ItemType Directory -Path $checkDirectory -Force | Out-Null
$dlls = @(Get-ChildItem -LiteralPath $UnityManagedDirectory -Filter '*.dll')
foreach ($name in @('UnityEngine.UI','Unity.TextMeshPro','Unity.TextMeshPro.Editor','UnityEditor.UI','Unity.InputSystem','Unity.Cinemachine','Unity.AI.Navigation','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime','Unity.Mathematics','Unity.Splines','Unity.Collections','Unity.Burst','Unity.VisualScripting.Core')) {
    $dlls += Get-Item -LiteralPath (Join-Path $projectRoot "Library/ScriptAssemblies/$name.dll")
}
$nunit = Get-ChildItem -Path (Join-Path $projectRoot 'Library/PackageCache/com.unity.ext.nunit*') -Filter 'nunit.framework.dll' -Recurse | Select-Object -First 1
if ($nunit) { $dlls += $nunit }
$references = ($dlls | ForEach-Object { '<Reference Include="' + $_.BaseName + '"><HintPath>' + [Security.SecurityElement]::Escape($_.FullName) + '</HintPath></Reference>' }) -join "`n"
$sourceGlob = [Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets/Scripts/**/*.cs'))
$xml = @"
<Project>
<PropertyGroup><BaseIntermediateOutputPath>$checkDirectory/obj/</BaseIntermediateOutputPath></PropertyGroup>
<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
<PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo><LangVersion>9.0</LangVersion><DefineConstants>UNITY_EDITOR;ENABLE_INPUT_SYSTEM</DefineConstants><OutputPath>$checkDirectory/bin/</OutputPath><NoWarn>0649</NoWarn></PropertyGroup>
<ItemGroup><Compile Include="$sourceGlob" /></ItemGroup><ItemGroup>$references</ItemGroup>
<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
</Project>
"@
$checkProject = Join-Path $checkDirectory 'MiniGameCompileChecks.csproj'
[IO.File]::WriteAllText($checkProject, $xml, [Text.UTF8Encoding]::new($false))
dotnet build $checkProject --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Unity-reference compilation failed.' }
