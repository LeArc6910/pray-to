<#
.SYNOPSIS
    Unity 프로젝트 설정과 확인을 명령 한 줄로 한다 (Windows).

.DESCRIPTION
    1. git pull, Git LFS 연결
    2. ProjectSettings/ProjectVersion.txt의 Unity 버전을 찾는다. 없으면 -InstallEditor로 Unity Hub를 통해 설치한다
    3. 배치 모드로 프로젝트를 열어 Game > Set Up Project를 실행한다
       (Active Input Handling, Product Name, 2D 모드, URP 2D 렌더러, Main 씬, GameSheets 에셋)
    4. EditMode 테스트(Core 규칙)와 PlayMode 테스트(실제 클라이언트로 혼자 하기와 호스트를 몇 초씩)를 돌린다
    5. 결과를 LOCAL_TASKS.md의 "결과 보고"에 적는다. -Commit이면 생성된 파일과 함께 커밋하고 푸시한다

    로그와 테스트 결과는 Logs/에 남는다(커밋하지 않는다). macOS와 Linux는 setup.sh를 쓴다.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File DevTools\Local\setup.ps1 -Commit
#>
[CmdletBinding()]
param(
    # 생성된 파일과 결과 보고를 커밋하고 푸시한다
    [switch]$Commit,
    # 이 버전의 Unity가 없으면 Unity Hub로 설치한다 (Web Build Support 포함, 몇 GB)
    [switch]$InstallEditor,
    # 테스트를 건너뛴다
    [switch]$SkipTests,
    # 시작할 때 git pull을 하지 않는다
    [switch]$NoPull
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Say([string]$text) { Write-Host "[setup] $text" -ForegroundColor Cyan }
function Warn([string]$text) { Write-Host "[setup] $text" -ForegroundColor Yellow }

# git을 부르고 출력 줄을 돌려준다. 종료 코드는 $script:GitExit에 남는다. Windows PowerShell 5.1은 Stop 모드에서
# 네이티브 명령의 stderr를 예외로 바꾸므로 잠시 Continue로 둔다.
function Invoke-Git {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & git -C $root @args 2>&1 | ForEach-Object { "$_" } }
    finally { $ErrorActionPreference = $previous }
    $script:GitExit = $LASTEXITCODE
    return $output
}

$versionText = Get-Content (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Raw
if ($versionText -notmatch 'm_EditorVersionWithRevision:\s*(\S+)\s*\((\w+)\)') { throw 'ProjectVersion.txt에서 Unity 버전을 읽지 못했습니다.' }
$version = $Matches[1]
$changeset = $Matches[2]

function Find-Unity {
    if ($env:UNITY_EXE) {
        if (Test-Path $env:UNITY_EXE) { return $env:UNITY_EXE }
        throw "UNITY_EXE가 가리키는 파일이 없습니다: $env:UNITY_EXE"
    }
    $folders = @()
    # Unity Hub > Settings > Installs에서 설치 위치를 바꿨다면 여기에 적혀 있다
    if ($env:APPDATA) {
        $custom = Join-Path $env:APPDATA 'UnityHub/secondaryInstallPath.json'
        if (Test-Path $custom) {
            $folder = Get-Content $custom -Raw | ConvertFrom-Json
            if ($folder) { $folders += $folder }
        }
    }
    if ($env:ProgramFiles) { $folders += Join-Path $env:ProgramFiles 'Unity/Hub/Editor' }
    foreach ($folder in $folders) {
        $exe = Join-Path $folder "$version/Editor/Unity.exe"
        if (Test-Path $exe) { return $exe }
    }
    return $null
}

function Get-Hub {
    if (-not $env:ProgramFiles) { return $null }
    $hub = Join-Path $env:ProgramFiles 'Unity Hub/Unity Hub.exe'
    if (Test-Path $hub) { return $hub }
    return $null
}

# Unity Hub의 명령줄 모드. 끝날 때까지 기다린다.
function Invoke-Hub([string]$arguments) {
    $hub = Get-Hub
    if (-not $hub) { throw 'Unity Hub가 없습니다. https://unity.com/download 에서 설치하고 로그인한 뒤 다시 실행하세요.' }
    $process = Start-Process -FilePath $hub -ArgumentList "-- --headless $arguments" -NoNewWindow -PassThru
    $null = $process.Handle
    $process.WaitForExit()
}

# 배치 모드로 Unity를 실행하고 종료 코드를 돌려준다. 로그는 Logs/<name>.log.
function Invoke-Unity([string]$name, [string[]]$extra) {
    $log = Join-Path $logs "$name.log"
    $arguments = @('-batchmode', '-projectPath', $root, '-logFile', $log) + $extra
    $line = ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $process = Start-Process -FilePath $script:unity -ArgumentList $line -NoNewWindow -PassThru
    # Handle을 한 번 읽어 둬야 기다린 뒤 ExitCode를 읽을 수 있다
    $null = $process.Handle
    $process.WaitForExit()
    return $process.ExitCode
}

# 로그에서 원인이 될 만한 줄을 뽑는다
function Get-LogErrors([string]$name) {
    $log = Join-Path $logs "$name.log"
    if (-not (Test-Path $log)) { return @() }
    $patterns = @('error CS\d+', 'Exception', 'Aborting batchmode', 'No valid Unity Editor license', 'Licensing::\w+\] Error',
        'exit with return code', 'Multiple Unity instances', 'compiler errors', 'executeMethod (method|class)')
    return @(Select-String -Path $log -Pattern $patterns | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -First 15)
}

# NUnit 결과 XML을 읽는다
function Read-TestResults([string]$name) {
    $path = Join-Path $logs "$name-results.xml"
    if (-not (Test-Path $path)) { return $null }
    [xml]$xml = Get-Content $path -Raw -Encoding UTF8
    $run = $xml.SelectSingleNode('/test-run')
    $failures = @($xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        $message = $_.SelectSingleNode('failure/message')
        $first = if ($message) { ($message.InnerText.Trim() -split "`n")[0].Trim() } else { '' }
        "$($_.GetAttribute('fullname')): $first"
    })
    return [pscustomobject]@{
        Total = [int]$run.GetAttribute('total')
        Passed = [int]$run.GetAttribute('passed')
        Failed = [int]$run.GetAttribute('failed')
        Failures = $failures
    }
}

function Get-PackageVersions {
    $lockFile = Join-Path $root 'Packages/packages-lock.json'
    if (-not (Test-Path $lockFile)) { return '' }
    $dependencies = (Get-Content $lockFile -Raw -Encoding UTF8 | ConvertFrom-Json).dependencies
    $names = @('com.unity.netcode.gameobjects', 'com.unity.transport', 'com.unity.render-pipelines.universal', 'com.unity.inputsystem',
        'com.unity.multiplayer.playmode', 'com.unity.test-framework')
    return (($names | ForEach-Object { $package = $dependencies.$_; if ($package) { "$_ $($package.version)" } }) -join ', ')
}

# --- 1. 저장소
if (-not $NoPull) {
    Say 'git pull'
    Invoke-Git pull --ff-only | ForEach-Object { Write-Host "  $_" }
    if ($script:GitExit -ne 0) { Warn 'git pull이 실패했습니다. 지금 있는 파일로 계속합니다.' }
}
Invoke-Git lfs install --local | Out-Null
if ($script:GitExit -ne 0) { Warn 'Git LFS가 없습니다. 이미지·소리 파일을 커밋하려면 https://git-lfs.com 에서 설치하세요.' }

# --- 2. Unity
$script:unity = Find-Unity
if (-not $script:unity) {
    if (-not $InstallEditor) {
        throw "Unity $version 을(를) 찾지 못했습니다. Unity Hub에서 설치하거나 -InstallEditor를 붙여 다시 실행하세요. 다른 곳에 있다면 환경 변수 UNITY_EXE에 Unity.exe 경로를 넣으세요."
    }
    Say "Unity $version 설치 중 (Web Build Support 포함). 몇 GB를 받으므로 오래 걸립니다."
    Invoke-Hub "install --version $version --changeset $changeset --module webgl"
    $script:unity = Find-Unity
    if (-not $script:unity) { throw "Unity $version 설치가 끝나지 않았습니다. Unity Hub를 열어 확인하세요." }
}
Say "Unity: $script:unity"
$webSupport = Join-Path (Split-Path $script:unity) 'Data/PlaybackEngines/WebGLSupport'
if (-not (Test-Path $webSupport)) {
    if ($InstallEditor) {
        Say 'Web Build Support 설치 중'
        Invoke-Hub "install-modules --version $version --module webgl"
    }
    else {
        Warn 'Web Build Support 모듈이 없습니다. 웹 빌드에 필요합니다. -InstallEditor를 붙이거나 Unity Hub > Installs에서 추가하세요.'
    }
}

$lock = Join-Path $root 'Temp/UnityLockfile'
if (Test-Path $lock) {
    try { [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close() }
    catch { throw 'Unity 에디터에서 이 프로젝트가 열려 있습니다. 에디터를 닫고 다시 실행하세요.' }
}

# --- 3. 프로젝트 설정
Say '프로젝트 설정 중. 처음에는 패키지를 받고 임포트하느라 10분쯤 걸릴 수 있습니다. (Logs/setup.log)'
$setupCode = Invoke-Unity 'setup' @('-quit', '-executeMethod', 'Game.Client.Editor.ProjectSetup.RunBatch')
$setupOk = $setupCode -eq 0
if ($setupOk) { Say '프로젝트 설정 완료' } else { Warn "프로젝트 설정 실패 (종료 코드 $setupCode)" }

# --- 4. 테스트
$results = [ordered]@{}
if ($setupOk -and -not $SkipTests) {
    foreach ($platform in @('EditMode', 'PlayMode')) {
        $name = $platform.ToLowerInvariant()
        Say "$platform 테스트 중 (Logs/$name.log)"
        $resultsFile = Join-Path $logs "$name-results.xml"
        if (Test-Path $resultsFile) { Remove-Item $resultsFile }
        $code = Invoke-Unity $name @('-runTests', '-testPlatform', $platform, '-testResults', $resultsFile)
        $results[$platform] = [pscustomobject]@{ Code = $code; Result = (Read-TestResults $name) }
    }
}

# --- 5. 결과 보고
$allOk = $setupOk
$report = New-Object System.Collections.Generic.List[string]
$summary = New-Object System.Collections.Generic.List[string]
$report.Add("### $(Get-Date -Format 'yyyy-MM-dd HH:mm') setup.ps1 (Unity $version)")
$report.Add('')
if ($setupOk) {
    $report.Add('- 프로젝트 설정: 성공')
    $summary.Add('Project setup: ok.')
}
else {
    $report.Add("- 프로젝트 설정: **실패** (종료 코드 $setupCode). Logs/setup.log에서 뽑은 줄:")
    foreach ($line in (Get-LogErrors 'setup')) { $report.Add("  - ``$line``") }
    if (Select-String -Path (Join-Path $logs 'setup.log') -Pattern 'Licensing::\w+\] Error' -Quiet) {
        $report.Add('- 라이선스 문제로 보인다. Unity Hub에 다시 로그인하고 Preferences > Licenses에서 Personal 라이선스를 확인한 뒤 다시 실행한다.')
    }
    $summary.Add("Project setup: failed with exit code $setupCode.")
}
$packages = Get-PackageVersions
if ($packages) { $report.Add("- 패키지: $packages") }
foreach ($platform in $results.Keys) {
    $entry = $results[$platform]
    $result = $entry.Result
    if ($null -eq $result) {
        $allOk = $false
        $report.Add("- $platform 테스트: **결과 파일이 없습니다** (종료 코드 $($entry.Code)). Logs/$($platform.ToLowerInvariant()).log에서 뽑은 줄:")
        foreach ($line in (Get-LogErrors $platform.ToLowerInvariant())) { $report.Add("  - ``$line``") }
        $summary.Add("${platform}: no results (exit code $($entry.Code)).")
        continue
    }
    if ($result.Failed -gt 0) { $allOk = $false }
    $report.Add("- $platform 테스트: $($result.Total)개 중 $($result.Passed)개 통과, $($result.Failed)개 실패")
    foreach ($failure in $result.Failures) { $report.Add("  - ``$failure``") }
    $summary.Add("${platform}: $($result.Passed)/$($result.Total) passed.")
}
if ($SkipTests) { $report.Add('- 테스트: 건너뜀') }
# 로컬은 스크립트를 고치지 않는다. Unity가 API 업데이트로 .cs를 바꿨다면 보고하고 커밋에서 뺀다.
# '--'는 따옴표로 감싸야 PowerShell이 삼키지 않고 git에 넘긴다.
$changedScripts = @(Invoke-Git diff HEAD --name-only '--' '*.cs')
if ($changedScripts.Count -gt 0) {
    $allOk = $false
    $report.Add("- Unity가 바꾼 스크립트 (커밋하지 않음): $($changedScripts -join ', ')")
    $summary.Add("Unity changed scripts, left uncommitted: $($changedScripts -join ', ').")
}

$tasksPath = Join-Path $root 'LOCAL_TASKS.md'
$tasks = [IO.File]::ReadAllText($tasksPath, [Text.Encoding]::UTF8)
$newline = if ($tasks.Contains("`r`n")) { "`r`n" } else { "`n" }
$block = ($report -join $newline) + $newline
if ($tasks.Contains('(아직 없음)')) { $tasks = $tasks.Replace('(아직 없음)' + $newline, $block) }
else { $tasks = $tasks.TrimEnd() + $newline + $newline + $block }
[IO.File]::WriteAllText($tasksPath, $tasks, (New-Object System.Text.UTF8Encoding $false))

Write-Host ''
$report | ForEach-Object { Write-Host $_ }
Write-Host ''

# --- 6. 커밋
if ($Commit) {
    Invoke-Git add -A | Out-Null
    if ($changedScripts.Count -gt 0) { Invoke-Git reset -q '--' @changedScripts | Out-Null }
    Invoke-Git commit -q -m 'Record local Unity setup results' -m ($summary -join ' ') | Out-Null
    if ($script:GitExit -eq 0) {
        Say 'git push'
        Invoke-Git push | ForEach-Object { Write-Host "  $_" }
        if ($script:GitExit -ne 0) { Warn 'git push가 실패했습니다. 직접 푸시하세요.'; $allOk = $false }
    }
    else { Say '커밋할 변경이 없습니다.' }
}
else {
    Say '결과를 LOCAL_TASKS.md에 적었습니다. -Commit을 붙이면 커밋하고 푸시까지 합니다.'
}

if ($allOk) {
    Say '끝. Unity Hub에서 프로젝트를 열고 Main 씬에서 Play를 누르면 됩니다.'
    exit 0
}
Warn '실패한 단계가 있습니다. 위 결과를 확인하세요.'
exit 1
