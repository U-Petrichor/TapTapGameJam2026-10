$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$target = Join-Path $projectRoot 'Temp/IntroSmokeProject'
foreach ($relative in @('Assets/Editor', 'Assets/Resources', 'Assets/TextMesh Pro', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $target $relative) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/UI/IntroVideoTransition.cs') -Destination (Join-Path $target 'Assets/IntroVideoTransition.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Core/GamePauseManager.cs') -Destination (Join-Path $target 'Assets/GamePauseManager.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Core/EventPipeline.cs') -Destination (Join-Path $target 'Assets/EventPipeline.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/Model/Video/FirstPlayVideo.mp4') -Destination (Join-Path $target 'Assets/Resources/Intro.mp4')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/TextMesh Pro/Resources') -Destination (Join-Path $target 'Assets/TextMesh Pro') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SmokeRunner.cs') -Destination (Join-Path $target 'Assets/IntroSmokeRunner.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SmokeEditor.cs') -Destination (Join-Path $target 'Assets/Editor/IntroSmokeEditor.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $target 'ProjectSettings/ProjectVersion.txt')
$original = Get-Content (Join-Path $projectRoot 'Packages/manifest.json') -Raw | ConvertFrom-Json
$deps = [ordered]@{}
foreach ($entry in $original.dependencies.PSObject.Properties) {
    if ($entry.Name.StartsWith('com.unity.modules.')) { $deps[$entry.Name] = $entry.Value }
}
$uiPackage = Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Directory -Filter 'com.unity.ugui@*' | Select-Object -First 1
$deps['com.unity.ugui'] = 'file:' + $uiPackage.FullName.Replace('\', '/')
@{ dependencies = $deps } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $target 'Packages/manifest.json') -Encoding utf8
@'
namespace TapTapGameJam.Events {
    public interface IEvent {}
    public struct ESCMenuEvent : IEvent { public bool IsOpen; }
    public struct EventContainerStateChanged : IEvent {}
}
namespace TapTapGameJam.Types { }
namespace TapTapGameJam.UI {
    public class UIManager {
        public static UIManager Instance = new UIManager();
        public bool AnyContainerOpen;
    }
}
'@ | Set-Content (Join-Path $target 'Assets/Stubs.cs') -Encoding utf8
Write-Output $target
