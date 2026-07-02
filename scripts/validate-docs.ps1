# Copyright (C) 2026 SharpEmu Emulator Project
# SPDX-License-Identifier: GPL-2.0-or-later

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$allowedStatuses = @(
    "Not Tested",
    "Loadable",
    "Booting",
    "Intro",
    "Menu",
    "In Game",
    "Playable",
    "Broken"
)

$allowedSeverities = @(
    "BLOCKER",
    "CRITICAL",
    "VISIBLE",
    "COSMETIC",
    "UNKNOWN"
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$compatibilityPath = Join-Path $repositoryRoot "docs/compatibility.md"
$gameNotesDirectory = Join-Path $repositoryRoot "docs/game-notes"
$errors = [System.Collections.Generic.List[string]]::new()
$referencedNotes = 0

function Get-MarkdownTableCells {
    param([Parameter(Mandatory)][string] $Line)

    return @(
        $Line.Trim().Trim("|").Split("|") |
            ForEach-Object { $_.Trim() }
    )
}

function Test-MarkdownSeparatorRow {
    param([Parameter(Mandatory)][string[]] $Cells)

    foreach ($cell in $Cells) {
        if ($cell -notmatch "^:?-{3,}:?$") {
            return $false
        }
    }

    return $true
}

if (-not (Test-Path -LiteralPath $compatibilityPath -PathType Leaf)) {
    throw "Missing compatibility matrix: $compatibilityPath"
}

if (-not (Test-Path -LiteralPath $gameNotesDirectory -PathType Container)) {
    throw "Missing game-notes directory: $gameNotesDirectory"
}

$compatibilityLines = Get-Content -LiteralPath $compatibilityPath
$compatibilityRows = @(
    $compatibilityLines |
        Where-Object { $_ -match "^\|\s*(PPSA\d{5})\s*\|" }
)

foreach ($row in $compatibilityRows) {
    $cells = Get-MarkdownTableCells -Line $row
    if ($cells.Count -lt 8) {
        $errors.Add("Compatibility row has fewer than eight columns: $row")
        continue
    }

    $titleId = $cells[0]
    $status = $cells[3].Trim([char]0x60)

    if ($titleId -cnotmatch "^PPSA\d{5}$") {
        $errors.Add("Title ID must be uppercase and match PPSA##### : $titleId")
    }

    if ($status -cnotin $allowedStatuses) {
        $errors.Add("Invalid compatibility status '$status' for $titleId")
    }

    $notesCell = $cells[7]
    $linkMatch = [regex]::Match($notesCell, "\((game-notes/[^)]+\.md)\)")
    if (-not $linkMatch.Success) {
        $errors.Add("Missing game-note link for $titleId")
        continue
    }

    $relativeNotesPath = $linkMatch.Groups[1].Value.Replace("/", [IO.Path]::DirectorySeparatorChar)
    $notesPath = Join-Path (Join-Path $repositoryRoot "docs") $relativeNotesPath
    $expectedFileName = "$titleId.md"

    if ([IO.Path]::GetFileName($notesPath) -cne $expectedFileName) {
        $errors.Add("Game-note file for $titleId must be named $expectedFileName")
    }

    if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) {
        $errors.Add("Referenced game-note file does not exist: $notesPath")
        continue
    }

    $referencedNotes++
    $statusLine = Get-Content -LiteralPath $notesPath |
        Where-Object { $_ -match "^- Compatibility Status:\s*(.+)$" } |
        Select-Object -First 1

    if ($null -eq $statusLine) {
        $errors.Add("Game-note file has no Compatibility Status: $notesPath")
        continue
    }

    $noteStatus = ([regex]::Match($statusLine, "^- Compatibility Status:\s*(.+)$")).Groups[1].Value.Trim().Trim([char]0x60)
    if ($noteStatus -cnotin $allowedStatuses) {
        $errors.Add("Invalid game-note status '$noteStatus' in $notesPath")
    }

    if ($noteStatus -cne $status) {
        $errors.Add("Status mismatch for ${titleId}: matrix='$status', note='$noteStatus'")
    }
}

$severityFiles = @(
    Get-ChildItem -LiteralPath $gameNotesDirectory -Filter "*.md" -File
)

$kernelStatusPath = Join-Path $repositoryRoot "docs/kernel-hle-status.md"
if (Test-Path -LiteralPath $kernelStatusPath -PathType Leaf) {
    $severityFiles += Get-Item -LiteralPath $kernelStatusPath
}

foreach ($file in $severityFiles) {
    $lines = Get-Content -LiteralPath $file.FullName
    $severityColumn = -1

    foreach ($line in $lines) {
        if ($line -notmatch "^\|") {
            $severityColumn = -1
            continue
        }

        $cells = Get-MarkdownTableCells -Line $line
        if ($severityColumn -lt 0) {
            $severityColumn = [Array]::IndexOf($cells, "Severity")
            continue
        }

        if (Test-MarkdownSeparatorRow -Cells $cells) {
            continue
        }

        if ($cells.Count -le $severityColumn) {
            $errors.Add("Malformed severity table row in $($file.FullName): $line")
            continue
        }

        $severity = $cells[$severityColumn].Trim([char]0x60)
        if ($severity -and $severity -cnotin $allowedSeverities) {
            $errors.Add("Invalid severity '$severity' in $($file.FullName)")
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) {
        Write-Error $validationError
    }

    exit 1
}

Write-Host "Documentation validation passed: $($compatibilityRows.Count) compatibility rows, $referencedNotes referenced notes."
