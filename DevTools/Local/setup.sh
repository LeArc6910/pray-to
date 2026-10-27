#!/usr/bin/env bash
# Unity 프로젝트 설정과 확인을 명령 한 줄로 한다 (macOS, Linux). Windows는 setup.ps1을 쓴다. 하는 일은 같다.
#
#   1. git pull, Git LFS 연결
#   2. ProjectSettings/ProjectVersion.txt의 Unity 버전을 찾는다. 없으면 --install-editor로 Unity Hub를 통해 설치한다
#   3. 배치 모드로 프로젝트를 열어 Game > Set Up Project를 실행한다
#      (Active Input Handling, Product Name, 2D 모드, URP 2D 렌더러, Main 씬, GameSheets 에셋)
#   4. EditMode 테스트(Core 규칙)와 PlayMode 테스트(실제 클라이언트로 혼자 하기와 호스트를 몇 초씩)를 돌린다
#   5. 결과를 LOCAL_TASKS.md의 "결과 보고"에 적는다. --commit이면 생성된 파일과 함께 커밋하고 푸시한다
#
# 사용법: DevTools/Local/setup.sh [--commit] [--install-editor] [--skip-tests] [--no-pull]
# Unity가 다른 곳에 있으면 환경 변수 UNITY_EXE에 실행 파일 경로를 넣는다. 로그와 테스트 결과는 Logs/에 남는다.
# macOS의 bash 3.2는 set -u에서 빈 배열을 쓰면 멈추므로 -u는 켜지 않는다.
set -o pipefail

commit=0
install_editor=0
skip_tests=0
pull=1
for arg in "$@"; do
    case "$arg" in
        --commit) commit=1 ;;
        --install-editor) install_editor=1 ;;
        --skip-tests) skip_tests=1 ;;
        --no-pull) pull=0 ;;
        *) echo "알 수 없는 옵션: $arg" >&2; exit 2 ;;
    esac
done

root="$(cd "$(dirname "$0")/../.." && pwd)"
logs="$root/Logs"
mkdir -p "$logs"

say() { printf '\033[36m[setup] %s\033[0m\n' "$1"; }
warn() { printf '\033[33m[setup] %s\033[0m\n' "$1" >&2; }
fail() { warn "$1"; exit 1; }

version_line="$(grep m_EditorVersionWithRevision "$root/ProjectSettings/ProjectVersion.txt")"
version="$(echo "$version_line" | sed -E 's/.*: ([^ ]+) \(([0-9a-f]+)\).*/\1/')"
changeset="$(echo "$version_line" | sed -E 's/.*: ([^ ]+) \(([0-9a-f]+)\).*/\2/')"

case "$(uname -s)" in
    Darwin)
        hub_config="$HOME/Library/Application Support/UnityHub/secondaryInstallPath.json"
        default_folder="/Applications/Unity/Hub/Editor"
        editor_binary="Unity.app/Contents/MacOS/Unity"
        hub="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"
        ;;
    *)
        hub_config="$HOME/.config/UnityHub/secondaryInstallPath.json"
        default_folder="$HOME/Unity/Hub/Editor"
        editor_binary="Editor/Unity"
        hub="$(command -v unityhub || true)"
        ;;
esac

find_unity() {
    if [ -n "${UNITY_EXE:-}" ]; then
        [ -x "$UNITY_EXE" ] || fail "UNITY_EXE가 가리키는 파일이 없습니다: $UNITY_EXE"
        echo "$UNITY_EXE"
        return
    fi
    local folders=()
    # Unity Hub > Settings > Installs에서 설치 위치를 바꿨다면 여기에 적혀 있다
    if [ -f "$hub_config" ]; then
        local custom
        custom="$(sed -e 's/^[[:space:]]*"//' -e 's/"[[:space:]]*$//' "$hub_config")"
        [ -n "$custom" ] && folders+=("$custom")
    fi
    folders+=("$default_folder")
    local folder
    for folder in "${folders[@]}"; do
        if [ -x "$folder/$version/$editor_binary" ]; then
            echo "$folder/$version/$editor_binary"
            return
        fi
    done
}

# Unity Hub의 명령줄 모드
run_hub() {
    if [ -z "$hub" ] || [ ! -x "$hub" ]; then
        fail "Unity Hub가 없습니다. https://unity.com/download 에서 설치하고 로그인한 뒤 다시 실행하세요."
    fi
    local architecture=()
    [ "$(uname -s)" = Darwin ] && [ "$(uname -m)" = arm64 ] && architecture=(--architecture arm64)
    "$hub" -- --headless "$@" "${architecture[@]}"
}

# 배치 모드로 Unity를 실행하고 종료 코드를 돌려준다. 로그는 Logs/<name>.log.
run_unity() {
    local name="$1"
    shift
    "$unity" -batchmode -projectPath "$root" -logFile "$logs/$name.log" "$@"
}

# 로그에서 원인이 될 만한 줄을 뽑는다
log_errors() {
    [ -f "$logs/$1.log" ] || return 0
    grep -E 'error CS[0-9]+|Exception|Aborting batchmode|No valid Unity Editor license|Licensing::[A-Za-z]+\] Error|exit with return code|Multiple Unity instances|compiler errors|executeMethod (method|class)' "$logs/$1.log" \
        | sed 's/^[[:space:]]*//' | awk '!seen[$0]++' | head -15
}

# NUnit 결과 XML의 test-run 속성 하나
run_attribute() {
    grep -o '<test-run [^>]*' "$1" | head -1 | grep -o " $2=\"[0-9]*\"" | grep -o '[0-9][0-9]*'
}

# 실패한 테스트마다 "이름: 메시지 첫 줄"
failures() {
    awk '
        /<test-case / && /result="Failed"/ {
            name = $0; sub(/.* fullname="/, "", name); sub(/".*/, "", name); want = 1; next
        }
        # 메시지는 CDATA 안에 있고, 첫 줄이 비어 있을 수 있다
        want && /<message>/ { text = $0; sub(/.*CDATA\[/, "", text); waiting = 1 }
        want && waiting && !/<message>/ { text = $0 }
        want && waiting {
            sub(/\]\]>.*/, "", text); gsub(/^[[:space:]]+|[[:space:]]+$/, "", text)
            if (text != "") { print name ": " text; want = 0; waiting = 0 }
        }
    ' "$1"
}

# 줄마다 "  - `줄`" 목록 항목으로 만든다
# shellcheck disable=SC2016
code_bullets() { sed 's/.*/  - `&`/'; }

package_versions() {
    local lock_file="$root/Packages/packages-lock.json"
    [ -f "$lock_file" ] || return 0
    awk -v names="com.unity.netcode.gameobjects com.unity.transport com.unity.render-pipelines.universal com.unity.inputsystem com.unity.multiplayer.playmode com.unity.test-framework" '
        BEGIN { count = split(names, list, " ") }
        { for (i = 1; i <= count; i++) if (index($0, "\"" list[i] "\": {")) current = list[i] }
        current != "" && /"version":/ {
            match($0, /"version": "[^"]*"/); found[current] = substr($0, RSTART + 12, RLENGTH - 13); current = ""
        }
        END {
            for (i = 1; i <= count; i++) if (list[i] in found) out = out (out == "" ? "" : ", ") list[i] " " found[list[i]]
            print out
        }
    ' "$lock_file"
}

# --- 1. 저장소
if [ "$pull" = 1 ]; then
    say "git pull"
    git -C "$root" pull --ff-only || warn "git pull이 실패했습니다. 지금 있는 파일로 계속합니다."
fi
git -C "$root" lfs install --local >/dev/null 2>&1 || warn "Git LFS가 없습니다. 이미지·소리 파일을 커밋하려면 https://git-lfs.com 에서 설치하세요."

# --- 2. Unity
unity="$(find_unity)" || exit 1
if [ -z "$unity" ]; then
    [ "$install_editor" = 1 ] || fail "Unity $version 을(를) 찾지 못했습니다. Unity Hub에서 설치하거나 --install-editor를 붙여 다시 실행하세요. 다른 곳에 있다면 환경 변수 UNITY_EXE에 실행 파일 경로를 넣으세요."
    say "Unity $version 설치 중 (Web Build Support 포함). 몇 GB를 받으므로 오래 걸립니다."
    run_hub install --version "$version" --changeset "$changeset" --module webgl
    unity="$(find_unity)"
    [ -n "$unity" ] || fail "Unity $version 설치가 끝나지 않았습니다. Unity Hub를 열어 확인하세요."
fi
say "Unity: $unity"
case "$(uname -s)" in
    Darwin) web_support="$(dirname "$(dirname "$(dirname "$(dirname "$unity")")")")/PlaybackEngines/WebGLSupport" ;;
    *) web_support="$(dirname "$unity")/Data/PlaybackEngines/WebGLSupport" ;;
esac
if [ ! -d "$web_support" ]; then
    if [ "$install_editor" = 1 ]; then
        say "Web Build Support 설치 중"
        run_hub install-modules --version "$version" --module webgl
    else
        warn "Web Build Support 모듈이 없습니다. 웹 빌드에 필요합니다. --install-editor를 붙이거나 Unity Hub > Installs에서 추가하세요."
    fi
fi

# --- 3. 프로젝트 설정
say "프로젝트 설정 중. 처음에는 패키지를 받고 임포트하느라 10분쯤 걸릴 수 있습니다. (Logs/setup.log)"
run_unity setup -quit -executeMethod Game.Client.Editor.ProjectSetup.RunBatch
setup_code=$?
if [ "$setup_code" = 0 ]; then say "프로젝트 설정 완료"; else warn "프로젝트 설정 실패 (종료 코드 $setup_code)"; fi

# --- 4. 테스트
platforms=()
codes=()
if [ "$setup_code" = 0 ] && [ "$skip_tests" = 0 ]; then
    for platform in EditMode PlayMode; do
        name="$(echo "$platform" | tr '[:upper:]' '[:lower:]')"
        say "$platform 테스트 중 (Logs/$name.log)"
        rm -f "$logs/$name-results.xml"
        run_unity "$name" -runTests -testPlatform "$platform" -testResults "$logs/$name-results.xml"
        codes+=($?)
        platforms+=("$platform")
    done
fi

# --- 5. 결과 보고
all_ok=1
report="$logs/report.md"
summary=""
{
    echo "### $(date '+%Y-%m-%d %H:%M') setup.sh (Unity $version)"
    echo
} > "$report"
if [ "$setup_code" = 0 ]; then
    echo "- 프로젝트 설정: 성공" >> "$report"
    summary="Project setup: ok."
else
    all_ok=0
    echo "- 프로젝트 설정: **실패** (종료 코드 $setup_code). Logs/setup.log에서 뽑은 줄:" >> "$report"
    log_errors setup | code_bullets >> "$report"
    if grep -qE 'Licensing::[A-Za-z]+\] Error' "$logs/setup.log" 2> /dev/null; then
        echo "- 라이선스 문제로 보인다. Unity Hub에 다시 로그인하고 Preferences > Licenses에서 Personal 라이선스를 확인한 뒤 다시 실행한다." >> "$report"
    fi
    summary="Project setup: failed with exit code $setup_code."
fi
packages="$(package_versions)"
[ -n "$packages" ] && echo "- 패키지: $packages" >> "$report"
for i in "${!platforms[@]}"; do
    platform="${platforms[$i]}"
    name="$(echo "$platform" | tr '[:upper:]' '[:lower:]')"
    results="$logs/$name-results.xml"
    if [ ! -f "$results" ]; then
        all_ok=0
        echo "- $platform 테스트: **결과 파일이 없습니다** (종료 코드 ${codes[$i]}). Logs/$name.log에서 뽑은 줄:" >> "$report"
        log_errors "$name" | code_bullets >> "$report"
        summary="$summary $platform: no results (exit code ${codes[$i]})."
        continue
    fi
    total="$(run_attribute "$results" total)"
    passed="$(run_attribute "$results" passed)"
    failed="$(run_attribute "$results" failed)"
    [ "${failed:-0}" = 0 ] || all_ok=0
    echo "- $platform 테스트: ${total}개 중 ${passed}개 통과, ${failed}개 실패" >> "$report"
    failures "$results" | code_bullets >> "$report"
    summary="$summary $platform: $passed/$total passed."
done
[ "$skip_tests" = 1 ] && echo "- 테스트: 건너뜀" >> "$report"
# 로컬은 스크립트를 고치지 않는다. Unity가 API 업데이트로 .cs를 바꿨다면 보고하고 커밋에서 뺀다.
changed_scripts="$(git -C "$root" diff HEAD --name-only -- '*.cs' | paste -sd, - | sed 's/,/, /g')"
if [ -n "$changed_scripts" ]; then
    all_ok=0
    echo "- Unity가 바꾼 스크립트 (커밋하지 않음): $changed_scripts" >> "$report"
    summary="$summary Unity changed scripts, left uncommitted: $changed_scripts."
fi

tasks="$root/LOCAL_TASKS.md"
awk -v block="$report" '
    $0 == "(아직 없음)" { while ((getline line < block) > 0) print line; replaced = 1; next }
    { print }
    END { if (!replaced) { print ""; while ((getline line < block) > 0) print line } }
' "$tasks" > "$tasks.new" && mv "$tasks.new" "$tasks"

echo
cat "$report"
echo

# --- 6. 커밋
if [ "$commit" = 1 ]; then
    git -C "$root" add -A
    git -C "$root" diff HEAD --name-only -- '*.cs' | while read -r script; do git -C "$root" reset -q -- "$script"; done
    if git -C "$root" commit -q -m "Record local Unity setup results" -m "$summary"; then
        say "git push"
        git -C "$root" push || { warn "git push가 실패했습니다. 직접 푸시하세요."; all_ok=0; }
    else
        say "커밋할 변경이 없습니다."
    fi
else
    say "결과를 LOCAL_TASKS.md에 적었습니다. --commit을 붙이면 커밋하고 푸시까지 합니다."
fi

if [ "$all_ok" = 1 ]; then
    say "끝. Unity Hub에서 프로젝트를 열고 Main 씬에서 Play를 누르면 됩니다."
    exit 0
fi
warn "실패한 단계가 있습니다. 위 결과를 확인하세요."
exit 1
