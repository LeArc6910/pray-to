#!/usr/bin/env bash
# Runs setup.sh and setup.ps1 against fake-unity in a throwaway clone with its own remote, so the scripts can be
# checked without Unity. CI runs this; run it after changing either script. The PowerShell half needs pwsh.
set -eo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
# The scripts never read binary assets, and a CI checkout has no LFS objects to give the clone.
export GIT_LFS_SKIP_SMUDGE=1

git init -q --bare "$work/remote.git"
# CI checks out one commit; the remote has to accept a push of that shallow history.
git -C "$work/remote.git" config receive.shallowUpdate true
git clone -q "$root" "$work/clone"
cd "$work/clone"
git checkout -q -B main
# Test the working copies, so uncommitted edits are covered too.
mkdir -p DevTools/Local
cp "$root/DevTools/Local/setup.sh" "$root/DevTools/Local/setup.ps1" DevTools/Local/
cp "$root/LOCAL_TASKS.md" .
git config user.name test
git config user.email test@example.com
git remote set-url origin "$work/remote.git"
git add -A
git commit -q --allow-empty -m "Test base"
git push -q origin main
git branch -q -u origin/main
base="$(git rev-parse HEAD)"
export UNITY_EXE="$here/fake-unity"

failed=0
expect() {
    if ! grep -qF -- "$2" "$1"; then
        echo "  FAIL: expected '$2' in $1"
        failed=1
    fi
}

# Puts the clone and its remote back to the test base before each case.
reset() {
    git reset -q --hard "$base"
    git clean -qfd
    git push -q -f origin "$base:main"
}

# $1: label, $2: expected exit code, then the command
case_run() {
    local label="$1" want="$2"
    shift 2
    echo "== $label"
    local code=0
    "$@" > "$work/out.txt" 2>&1 || code=$?
    if [ "$code" != "$want" ]; then
        echo "  FAIL: exit code $code, expected $want"
        cat "$work/out.txt"
        failed=1
    fi
}

check_runner() {
    local name="$1" commit="$2"
    shift 2
    reset
    case_run "$name: everything passes" 0 "$@" "$commit"
    expect LOCAL_TASKS.md "EditMode 테스트: 140개 중 140개 통과, 0개 실패"
    expect LOCAL_TASKS.md "PlayMode 테스트: 2개 중 2개 통과, 0개 실패"
    expect LOCAL_TASKS.md "패키지: com.unity.netcode.gameobjects 2.13.3"
    git -C "$work/remote.git" log -1 --format=%s main > "$work/subject.txt"
    expect "$work/subject.txt" "Record local Unity setup results"
    git -C "$work/remote.git" show --stat --format= main > "$work/stat.txt"
    expect "$work/stat.txt" "Assets/_Project/Settings/URP-2D.asset"

    reset
    FAKE_PLAY_FAIL=1 FAKE_TOUCH_CS=1 case_run "$name: a play mode test fails and Unity changes a script" 1 "$@" "$commit"
    expect LOCAL_TASKS.md "PlayMode 테스트: 2개 중 1개 통과, 1개 실패"
    expect LOCAL_TASKS.md "Game.Client.Tests.SessionTests.HostSessionRuns: The host session never started playing."
    expect LOCAL_TASKS.md "Unity가 바꾼 스크립트 (커밋하지 않음): Assets/_Project/Scripts/Client/Palette.cs"
    git -C "$work/remote.git" show --stat --format= main > "$work/stat.txt"
    if grep -q "Palette.cs" "$work/stat.txt"; then
        echo "  FAIL: the changed script was committed"
        failed=1
    fi

    reset
    FAKE_LICENSE_FAIL=1 case_run "$name: Unity has no license" 1 "$@"
    expect LOCAL_TASKS.md "프로젝트 설정: **실패** (종료 코드 198)"
    expect LOCAL_TASKS.md "[Licensing::Module] Error: 'com.unity.editor.headless' was not found."
    expect LOCAL_TASKS.md "라이선스 문제로 보인다"

    reset
    FAKE_SETUP_FAIL=1 case_run "$name: the project does not compile" 1 "$@"
    expect LOCAL_TASKS.md "프로젝트 설정: **실패** (종료 코드 1)"
    expect LOCAL_TASKS.md "error CS0246: The type or namespace name 'Foo' could not be found"
}

check_runner setup.sh --commit bash DevTools/Local/setup.sh --no-pull
if command -v pwsh > /dev/null; then
    check_runner setup.ps1 -Commit pwsh -NoProfile -File DevTools/Local/setup.ps1 -NoPull
else
    echo "pwsh not found; skipped setup.ps1"
fi

if [ "$failed" = 0 ]; then echo "All setup script checks passed"; fi
exit "$failed"
