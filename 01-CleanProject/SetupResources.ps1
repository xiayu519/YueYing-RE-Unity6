$ErrorActionPreference = 'Stop'

$cleanRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $cleanRoot 'UnityProject'
$resourcePath = Join-Path $projectPath 'Assets\AssetRaw\Jxqy'
$resultPath = Join-Path $projectPath 'Temp\JxqyCleanSetup\setup.result'
$requestPath = Join-Path $projectPath 'Temp\JxqyCleanSetup\setup.request'
$progressPath = Join-Path $projectPath 'Temp\JxqyCleanSetup\setup.progress'
$logPath = Join-Path $projectPath 'Logs\JxqyCleanSetup.log'
$unityPath = $env:UNITY_EDITOR

if (-not (Test-Path -LiteralPath $resourcePath -PathType Container)) {
    throw 'Import JxqyResources.unitypackage before running setup.'
}

if ([string]::IsNullOrWhiteSpace($unityPath)) {
    $unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
}
if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) {
    throw 'Unity 6000.5.4f1 was not found. Set UNITY_EDITOR to Unity.exe.'
}

[System.IO.Directory]::CreateDirectory((Split-Path -Parent $resultPath)) | Out-Null
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $logPath)) | Out-Null
if ([System.IO.File]::Exists($resultPath)) {
    [System.IO.File]::Delete($resultPath)
}
if ([System.IO.File]::Exists($progressPath)) {
    [System.IO.File]::Delete($progressPath)
}

$lockPath = Join-Path $projectPath 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    Write-Host '[INFO] Unity Editor is open. Configuring Editor Simulate Mode...'
    [System.IO.File]::WriteAllText($requestPath, 'setup')
    $deadline = (Get-Date).AddMinutes(10)
    $lastProgress = ''
    while ((Get-Date) -lt $deadline -and
           -not [System.IO.File]::Exists($resultPath)) {
        if ([System.IO.File]::Exists($progressPath)) {
            $progress = [System.IO.File]::ReadAllText($progressPath).Trim()
            if ($progress -ne $lastProgress) {
                Write-Host "[INFO] $progress"
                $lastProgress = $progress
            }
        }
        Start-Sleep -Seconds 1
    }
    if (-not [System.IO.File]::Exists($resultPath)) {
        throw 'Timed out waiting for the Unity Editor setup request.'
    }
}
else {
    Write-Host '[INFO] Starting Unity 6000.5.4f1 to configure Editor Simulate Mode...'
    $arguments = @(
        '-batchmode',
        '-projectPath', ('"' + $projectPath + '"'),
        '-executeMethod',
        'Jxqy.Editor.Validation.JxqyCleanProjectSetup.ConfigureFromCommandLine',
        '-quit',
        '-logFile', ('"' + $logPath + '"')
    )
    $startParameters = @{
        FilePath = $unityPath
        ArgumentList = $arguments
        WindowStyle = 'Hidden'
        Wait = $true
        PassThru = $true
    }
    $process = Start-Process @startParameters
    if ($process.ExitCode -ne 0 -and
        -not [System.IO.File]::Exists($resultPath)) {
        throw "Unity setup failed with exit code $($process.ExitCode). See $logPath"
    }
}

if (-not [System.IO.File]::Exists($resultPath)) {
    throw "Unity did not create a setup result. See $logPath"
}

$result = [System.IO.File]::ReadAllText($resultPath)
if (-not $result.StartsWith('SUCCESS', [System.StringComparison]::Ordinal)) {
    throw "Resource setup failed:`n$result"
}

Write-Host '[SUCCESS] Editor Simulate Mode is ready. No bundles were built.'
Write-Host 'Open Assets/Scenes/main.unity and press Play.'
Write-Host $result
