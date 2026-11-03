#!/usr/bin/env bash
# Runs setup.sh and setup.ps1, and watch.sh and watch.ps1, against fake-unity in a throwaway clone with its own
# remote, so the scripts can be checked without Unity. CI runs this; run it after changing any of them. The PowerShell
# half needs pwsh.
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
cp "$root/DevTools/Local/setup.sh" "$root/DevTools/Local/setup.ps1" "$root/DevTools/Local/watch.sh" "$root/DevTools/Local/watch.ps1" DevTools/Local/
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
# The fake draws screenshots without a display.
export PRAY_TO_SCREENS=1

failed=0
expect() {
    if ! grep -qF -- "$2" "$1"; then
        echo "  FAIL: expected '$2' in $1"
        failed=1
    fi
}

# Puts the clone and its remote back to the test base before each case, without the logs, the watch's memory or
# the screenshots branch.
reset() {
    git reset -q --hard "$base"
    git clean -qfd
    rm -rf Logs
    git push -q -f origin "$base:main"
    git -C "$work/remote.git" branch -q -D local-screens 2> /dev/null || true
}

# The cloud pushes a commit to main, from a clone of its own.
cloud_push() {
    rm -rf "$work/cloud"
    git clone -q --branch main "$work/remote.git" "$work/cloud"
    (
        cd "$work/cloud"
        git config user.name cloud
        git config user.email cloud@example.com
        echo "$1" >> cloud.txt
        git add cloud.txt
        git commit -q -m "$1"
        git push -q origin HEAD:main
    )
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
    expect LOCAL_TASKS.md "화면 캡처: 사진 2장, 장면 7개 중 7개 통과"
    git -C "$work/remote.git" show --stat --format= local-screens > "$work/screens.txt" 2>&1
    expect "$work/screens.txt" "solo.jpg"
    expect "$work/screens.txt" "README.md"

    reset
    FAKE_SCREENS_HANG=1 PRAY_TO_SCREENS_SECONDS=4 case_run "$name: the screenshot run never ends" 1 "$@" "$commit"
    expect LOCAL_TASKS.md "화면 캡처: **4초 안에 끝나지 않아 껐다**"
    expect LOCAL_TASKS.md "NullReferenceException"

    reset
    FAKE_SCREENS_LINGER=1 PRAY_TO_SCREENS_GRACE=2 case_run "$name: the editor stays open after the screenshots" 0 "$@" "$commit"
    expect LOCAL_TASKS.md "화면 캡처: 사진 2장, 장면 7개 중 7개 통과"

    reset
    FAKE_PUSH_RACE=1 case_run "$name: the cloud pushes while the tests run" 0 "$@" "$commit"
    expect "$work/out.txt" "다시 푸시했다"
    git -C "$work/remote.git" log --format=%s -2 main > "$work/log.txt"
    expect "$work/log.txt" "Record local Unity setup results"
    expect "$work/log.txt" "Cloud change meanwhile"

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
    FAKE_EDIT_INCONCLUSIVE=1 case_run "$name: a test ends without a result" 0 "$@" "$commit"
    expect LOCAL_TASKS.md "EditMode 테스트: 140개 중 139개 통과, 0개 실패, **결과 없음 1개**"
    expect LOCAL_TASKS.md "결과 없음: Game.Core.Tests.ElementTests.ColdSlowsAMonsterDown: the frost bolt landed"

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

# $1: label, then the command that checks once
check_watch() {
    local name="$1"
    shift
    reset
    case_run "$name: a new commit is tested and the results pushed" 0 "$@"
    expect "$work/out.txt" "시험을 돌립니다"
    git -C "$work/remote.git" log -1 --format=%s main > "$work/subject.txt"
    expect "$work/subject.txt" "Record local Unity setup results"

    case_run "$name: then nothing is new" 0 "$@"
    expect "$work/out.txt" "새로 시험할 것이 없습니다."

    cloud_push "Cloud work"
    case_run "$name: the cloud's next commit is tested" 0 "$@"
    expect "$work/out.txt" "시험을 돌립니다"
    git -C "$work/remote.git" log --format=%s -3 main > "$work/log.txt"
    expect "$work/log.txt" "Cloud work"

    cloud_push "More cloud work"
    echo "edited by hand" >> LOCAL_TASKS.md
    case_run "$name: someone's uncommitted work is left alone" 0 "$@"
    expect "$work/out.txt" "커밋하지 않은 변경이 있어"
    git checkout -q -- LOCAL_TASKS.md
}

check_runner setup.sh --commit bash DevTools/Local/setup.sh --no-pull
check_watch watch.sh bash DevTools/Local/watch.sh --once
if command -v pwsh > /dev/null; then
    check_runner setup.ps1 -Commit pwsh -NoProfile -File DevTools/Local/setup.ps1 -NoPull
    check_watch watch.ps1 pwsh -NoProfile -File DevTools/Local/watch.ps1 -Once
else
    echo "pwsh not found; skipped setup.ps1 and watch.ps1"
fi

if [ "$failed" = 0 ]; then echo "All setup script checks passed"; fi
exit "$failed"
