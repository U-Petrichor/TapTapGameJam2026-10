param([string]$CodePath, [string]$Tool = 'Unity_RunCommand', [string]$ParamsPath, [ValidateRange(1,300)][int]$TimeoutSeconds = 45, [switch]$Worker)
$ErrorActionPreference = 'Stop'
# The deadline must cover discovery, connect and write too, not just response reads.
# Keep the potentially blocking pipe operations in a disposable, memory-limited worker.
if (-not $Worker) {
    $stdoutPath = [IO.Path]::GetTempFileName()
    $stderrPath = [IO.Path]::GetTempFileName()
    $workerProcess = $null
    try {
        $workerArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-Worker', '-TimeoutSeconds', $TimeoutSeconds, '-Tool', ('"' + $Tool + '"'))
        if ($CodePath) { $workerArgs += @('-CodePath', ('"' + (Resolve-Path -LiteralPath $CodePath).Path + '"')) }
        if ($ParamsPath) { $workerArgs += @('-ParamsPath', ('"' + (Resolve-Path -LiteralPath $ParamsPath).Path + '"')) }
        $workerProcess = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $workerArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        while (-not $workerProcess.WaitForExit(100)) {
            $workerProcess.Refresh()
            if ($deadline.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw 'Unity bridge exceeded its total time limit; worker terminated.' }
            if ($workerProcess.WorkingSet64 -gt 256MB) { throw 'Unity bridge exceeded its 256 MB memory limit; worker terminated.' }
        }
        if ((Get-Item -LiteralPath $stdoutPath).Length -gt 4MB -or (Get-Item -LiteralPath $stderrPath).Length -gt 4MB) { throw 'Unity bridge output exceeded 4 MB.' }
        $workerOutput = Get-Content -LiteralPath $stdoutPath -Raw
        if ($workerOutput) { Write-Output $workerOutput }
        if ($workerProcess.ExitCode -ne 0) { throw (Get-Content -LiteralPath $stderrPath -Raw) }
    } finally {
        if ($workerProcess -and -not $workerProcess.HasExited) { $workerProcess.Kill(); $workerProcess.WaitForExit() }
        if ($workerProcess) { $workerProcess.Dispose() }
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
    return
}
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
