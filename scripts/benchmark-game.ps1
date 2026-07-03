# Copyright (C) 2026 SharpEmu Emulator Project
# SPDX-License-Identifier: GPL-2.0-or-later

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^PPSA\d{5}$")]
    [string] $TitleId,

    [ValidateRange(1, 20)]
    [int] $Runs = 3,

    [ValidateRange(1, 600)]
    [int] $TimeoutSeconds = 30,

    [ValidateRange(0, 4096)]
    [int] $TraceImports = 128,

    [ValidateSet("trace", "debug", "info", "warn", "error")]
    [string] $LogLevel = "debug",

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$ebootPath = Join-Path $repositoryRoot "games\$TitleId\eboot.bin"
$cliProject = Join-Path $repositoryRoot "src\SharpEmu.CLI\SharpEmu.CLI.csproj"
$executablePath = Join-Path $repositoryRoot "artifacts\bin\$Configuration\net10.0\win-x64\SharpEmu.exe"

if (-not (Test-Path -LiteralPath $ebootPath -PathType Leaf)) {
    throw "Game executable not found: $ebootPath"
}

if (-not $NoBuild) {
    & dotnet build $cliProject -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "SharpEmu build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "SharpEmu executable not found: $executablePath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $repositoryRoot "artifacts\benchmarks\$TitleId\$timestamp"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$gameHash = (Get-FileHash -LiteralPath $ebootPath -Algorithm SHA256).Hash
$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$results = [System.Collections.Generic.List[object]]::new()
$quotedEbootPath = '"{0}"' -f $ebootPath.Replace('"', '\"')

for ($run = 1; $run -le $Runs; $run++) {
    $runName = "run-{0:D2}" -f $run
    $runDirectory = Join-Path $outputDirectory $runName
    New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

    $stdoutPath = Join-Path $runDirectory "stdout.log"
    $stderrPath = Join-Path $runDirectory "stderr.log"
    $knownProcessIds = @(
        Get-Process -Name "SharpEmu" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Id
    )

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @("--trace-imports=$TraceImports", "--log-level=$LogLevel", $quotedEbootPath) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru

    $peakWorkingSet = 0L
    $peakPrivateMemory = 0L
    $peakCpuSeconds = 0.0
    $timedOut = $false

    while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $runProcesses = @(
            Get-Process -Name "SharpEmu" -ErrorAction SilentlyContinue |
                Where-Object { $knownProcessIds -notcontains $_.Id }
        )
        foreach ($runProcess in $runProcesses) {
            try {
                $runProcess.Refresh()
                $peakWorkingSet = [Math]::Max($peakWorkingSet, $runProcess.WorkingSet64)
                $peakPrivateMemory = [Math]::Max($peakPrivateMemory, $runProcess.PrivateMemorySize64)
                $peakCpuSeconds = [Math]::Max(
                    $peakCpuSeconds,
                    $runProcess.TotalProcessorTime.TotalSeconds)
            }
            catch {
                # The mitigated child may exit between enumeration and sampling.
            }
        }

        Start-Sleep -Milliseconds 50
        $process.Refresh()
    }

    if (-not $process.HasExited) {
        $timedOut = $true
        Get-Process -Name "SharpEmu" -ErrorAction SilentlyContinue |
            Where-Object { $knownProcessIds -notcontains $_.Id } |
            Stop-Process -Force -ErrorAction SilentlyContinue
    }

    $process.WaitForExit()
    $stopwatch.Stop()
    $stderr = Get-Content -LiteralPath $stderrPath -Raw
    $importMatches = [regex]::Matches($stderr, "Import#(\d+)")
    $maximumImport = if ($importMatches.Count -eq 0) {
        0
    }
    else {
        ($importMatches | ForEach-Object { [int] $_.Groups[1].Value } |
            Measure-Object -Maximum).Maximum
    }
    $unresolvedNids = @(
        [regex]::Matches($stderr, "unresolved: nid=([^\s]+)") |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )
    $accessViolation = $stderr -match "NATIVE EXCEPTION CAUGHT|VEH_AV"
    $terminalState = if ($timedOut) {
        "timeout"
    }
    elseif ($accessViolation) {
        "access_violation"
    }
    elseif ($stderr -match "SharpEmu execution completed") {
        "completed"
    }
    else {
        "terminated"
    }

    $result = [ordered]@{
        run = $run
        duration_seconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        timed_out = $timedOut
        terminal_state = $terminalState
        exit_code = $process.ExitCode
        peak_working_set_mib = [Math]::Round($peakWorkingSet / 1MB, 1)
        peak_private_mib = [Math]::Round($peakPrivateMemory / 1MB, 1)
        peak_cpu_seconds = [Math]::Round($peakCpuSeconds, 3)
        maximum_import = $maximumImport
        unresolved_nid_count = $unresolvedNids.Count
        unresolved_nids = $unresolvedNids
        first_frame = $stderr -match "First frame|first VideoOut frame"
        access_violation = $accessViolation
        stdout_bytes = (Get-Item -LiteralPath $stdoutPath).Length
        stderr_bytes = (Get-Item -LiteralPath $stderrPath).Length
    }
    $result | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $runDirectory "metrics.json") -Encoding utf8
    $results.Add([pscustomobject] $result)

    Write-Host (
        "{0}: {1:N3}s, peak WS {2:N1} MiB, import #{3}, {4}" -f
            $runName,
            $result.duration_seconds,
            $result.peak_working_set_mib,
            $result.maximum_import,
            $result.terminal_state)
}

$orderedDurations = @($results.duration_seconds | Sort-Object)
$medianDuration = $orderedDurations[[Math]::Floor($orderedDurations.Count / 2)]
$summary = [ordered]@{
    title_id = $TitleId
    generated_at = (Get-Date).ToString("o")
    source_commit = $sourceCommit
    eboot_sha256 = $gameHash
    configuration = $Configuration
    run_count = $results.Count
    timeout_seconds = $TimeoutSeconds
    duration_seconds = [ordered]@{
        minimum = ($results.duration_seconds | Measure-Object -Minimum).Minimum
        median = $medianDuration
        maximum = ($results.duration_seconds | Measure-Object -Maximum).Maximum
        average = [Math]::Round(
            ($results.duration_seconds | Measure-Object -Average).Average,
            3)
    }
    maximum_peak_working_set_mib =
        ($results.peak_working_set_mib | Measure-Object -Maximum).Maximum
    maximum_peak_private_mib =
        ($results.peak_private_mib | Measure-Object -Maximum).Maximum
    highest_import_reached =
        ($results.maximum_import | Measure-Object -Maximum).Maximum
    first_frame_reached = $results.first_frame -contains $true
    runs = $results
}
$summaryPath = Join-Path $outputDirectory "summary.json"
$summary | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "Benchmark summary: $summaryPath"
