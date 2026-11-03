<#
.SYNOPSIS
    클라우드가 main에 새로 올린 것을 사람 손 없이 시험한다 (Windows).

.DESCRIPTION
    몇 분마다 origin/main을 보고, 마지막으로 시험한 뒤 새 커밋(로컬 결과 커밋이 아닌 것)이 있으면
    setup.ps1 -Commit을 돌린다: 받기, 프로젝트 설정, EditMode·PlayMode 테스트, 화면 캡처, 결과 보고와 푸시.
    이 컴퓨터에서 Unity가 이 프로젝트를 열고 있거나, 커밋하지 않은 변경이 있으면 그 차례는 건너뛴다.

    마지막으로 시험한 커밋은 Logs/watch-tested.txt, 기록은 Logs/watch.log에 남는다. macOS와 Linux는 watch.sh를 쓴다.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File DevTools\Local\watch.ps1 -Install
    로그인할 때마다 저절로 켜지게 하고 지금도 켠다. 끄려면 -Uninstall.
#>
[CmdletBinding()]
param(
    # 한 번만 보고 끝낸다
    [switch]$Once,
    # 몇 분마다 볼지
    [int]$Interval = 5,
    # 로그인할 때마다 최소화된 창으로 켜지게 시작프로그램에 넣고, 지금도 켠다
    [switch]$Install,
    # 시작프로그램에서 뺀다
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$state = Join-Path $logs 'watch-tested.txt'
$lockFile = Join-Path $logs 'watch.lock'
$resultsSubject = 'Record local Unity setup results'

function Say([string]$text) { Write-Host "[watch $(Get-Date -Format 'HH:mm')] $text" -ForegroundColor Cyan }
function Warn([string]$text) { Write-Host "[watch $(Get-Date -Format 'HH:mm')] $text" -ForegroundColor Yellow }

# git을 부르고 출력 줄을 돌려준다. 종료 코드는 $script:GitExit에 남는다(setup.ps1과 같다).
function Invoke-Git {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & git -C $root @args 2>&1 | ForEach-Object { "$_" } }
    finally { $ErrorActionPreference = $previous }
    $script:GitExit = $LASTEXITCODE
    return $output
}

if ($Install -or $Uninstall) {
    $startup = [Environment]::GetFolderPath('Startup')
    if (-not $startup) { throw '시작프로그램 폴더를 찾지 못했습니다. watch.ps1을 직접 띄워 두세요.' }
    $shortcut = Join-Path $startup 'pray-to-watch.cmd'
    if ($Uninstall) {
        if (Test-Path $shortcut) { Remove-Item $shortcut }
        Say '시작프로그램에서 뺐습니다. 지금 떠 있는 창은 닫으면 됩니다.'
        exit 0
    }
    $script = Join-Path $PSScriptRoot 'watch.ps1'
    $command = "start `"pray-to watch`" /min powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`""
    [IO.File]::WriteAllText($shortcut, "@echo off`r`n$command`r`n", (New-Object System.Text.UTF8Encoding $false))
    Say "로그인할 때마다 켜지도록 넣었습니다: $shortcut"
    Start-Process -FilePath 'cmd.exe' -ArgumentList "/c $command" -WindowStyle Minimized
    Say '지금도 켰습니다(작업 표시줄의 최소화된 창). 기록: Logs/watch.log'
    exit 0
}

# 한 번에 하나만 돈다
if (Test-Path $lockFile) {
    $other = Get-Content $lockFile -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($other -and (Get-Process -Id ([int]$other) -ErrorAction SilentlyContinue)) {
        Say "다른 watch가 이미 돌고 있습니다 (PID $other)."
        exit 0
    }
}
Set-Content -Path $lockFile -Value $PID

# 사람이 Unity로 이 프로젝트를 열고 있는지: 열려 있으면 Unity가 잠금 파일을 쥐고 있다
function Test-UnityOpen {
    $unityLock = Join-Path $root 'Temp/UnityLockfile'
    if (-not (Test-Path $unityLock)) { return $false }
    try { [IO.File]::Open($unityLock, 'Open', 'ReadWrite', 'None').Close(); return $false }
    catch { return $true }
}

function Write-WatchLog([string]$text) {
    Add-Content -Path (Join-Path $logs 'watch.log') -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm') $text" -Encoding UTF8
}

function Invoke-Check {
    Invoke-Git fetch -q origin main | Out-Null
    if ($script:GitExit -ne 0) { Warn 'git fetch가 실패했습니다. 다음 차례에 다시 봅니다.'; return }
    $remote = (Invoke-Git rev-parse origin/main | Select-Object -First 1)
    $last = if (Test-Path $state) { (Get-Content $state | Select-Object -First 1) } else { '' }
    if ($remote -eq $last) { Say '새로 시험할 것이 없습니다.'; return }
    # 그 뒤로 로컬 결과 커밋만 늘었으면 시험할 것이 없다
    if ($last) {
        Invoke-Git cat-file -e "$last^{commit}" | Out-Null
        if ($script:GitExit -eq 0) {
            $others = @(Invoke-Git log --format=%s "$last..$remote" | Where-Object { $_ -ne $resultsSubject })
            if ($others.Count -eq 0) {
                Set-Content -Path $state -Value $remote
                Say '새로 시험할 것이 없습니다.'
                return
            }
        }
    }
    if (@(Invoke-Git status --porcelain --untracked-files=no).Count -gt 0) {
        Warn '커밋하지 않은 변경이 있어 이번 차례는 건너뜁니다(사람이 작업 중일 수 있다).'
        return
    }
    if (Test-UnityOpen) { Warn 'Unity가 이 프로젝트를 열고 있어 이번 차례는 건너뜁니다.'; return }
    $subject = (Invoke-Git log -1 --format='%h %s' origin/main | Select-Object -First 1)
    Say "새 커밋 $subject. 시험을 돌립니다."
    Write-WatchLog "시험 시작: $subject"
    # The same PowerShell this runs in: Windows PowerShell or PowerShell 7 (pwsh).
    $shell = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
    & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'setup.ps1') -Commit
    $code = $LASTEXITCODE
    # 결과 커밋까지 넣은 지금을 기억한다. 실패해도 같은 커밋을 되풀이해 돌리지 않는다.
    Set-Content -Path $state -Value (Invoke-Git rev-parse HEAD | Select-Object -First 1)
    if ($code -eq 0) { Say '모두 통과. 결과를 올렸습니다.'; Write-WatchLog '모두 통과' }
    else { Warn '실패한 단계가 있습니다. LOCAL_TASKS.md의 결과 보고를 보세요.'; Write-WatchLog "실패 (종료 코드 $code)" }
}

try {
    while ($true) {
        Invoke-Check
        if ($Once) { break }
        Start-Sleep -Seconds ($Interval * 60)
    }
}
finally {
    Remove-Item $lockFile -ErrorAction SilentlyContinue
}
