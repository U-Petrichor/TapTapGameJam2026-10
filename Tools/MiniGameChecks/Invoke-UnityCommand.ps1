param([string]$CodePath, [string]$Tool = 'Unity_RunCommand', [string]$ParamsPath, [int]$TimeoutSeconds = 45)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$editorProcess = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($projectRoot) -and $_.CommandLine -notmatch 'AssetImportWorker'
} | Select-Object -First 1
if (-not $editorProcess) { throw 'No Unity Editor for this project is running.' }
$pipePath = [IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { $_ -match ('unity-mcp-.+-' + $editorProcess.ProcessId + '$') } | Select-Object -First 1
if (-not $pipePath) { throw 'Unity MCP bridge is unavailable.' }
$pipeName = $pipePath.Substring($pipePath.LastIndexOf('\') + 1)
$pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $pipeName, [IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(3000)
    $reader = [IO.StreamReader]::new($pipe, [Text.UTF8Encoding]::new($false))
    $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    $handshake = $reader.ReadLineAsync()
    if (-not $handshake.Wait(8000)) { throw 'Unity handshake timed out.' }
    if ($ParamsPath) { $toolParams = Get-Content -LiteralPath $ParamsPath -Raw | ConvertFrom-Json }
    else { $toolParams = @{ Code = Get-Content -LiteralPath $CodePath -Raw; Title = [IO.Path]::GetFileNameWithoutExtension($CodePath) } }
    $requestId = [Guid]::NewGuid().ToString()
    $writer.WriteLine((@{type=$Tool; params=$toolParams; requestId=$requestId} | ConvertTo-Json -Depth 20 -Compress))
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $read = $reader.ReadLineAsync()
        $remaining = [Math]::Max(1, [int](($TimeoutSeconds - $timer.Elapsed.TotalSeconds) * 1000))
        if (-not $read.Wait($remaining)) { throw 'Unity command timed out.' }
        if (-not $read.Result) { throw 'Unity disconnected (a script compilation may be in progress).' }
        $message = $read.Result | ConvertFrom-Json
        if ($message.type -in @('approval_pending','approval_denied')) { throw ('Unity connection requires approval: ' + $message.type) }
        if ($message.type -in @('command_in_progress','handshake')) { continue }
        [IO.File]::WriteAllText((Join-Path $projectRoot 'Temp/UnityLastResponse.json'), $read.Result)
        if ($message.status -eq 'error') { throw $message.error }
        $data = $message.result.data
        if ($data) {
            [pscustomobject]@{ success=$message.result.success; compiled=$data.isCompilationSuccessful; executed=$data.isExecutionSuccessful; compilationLogs=$data.compilationLogs; executionLogs=$data.executionLogs } | ConvertTo-Json -Depth 6
            if ($data.isCompilationSuccessful -eq $false -or $data.isExecutionSuccessful -eq $false) { throw 'Unity command failed; see Temp/UnityLastResponse.json.' }
        } else { $read.Result }
        break
    }
} finally { $pipe.Dispose() }
