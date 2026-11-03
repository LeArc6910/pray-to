#!/usr/bin/env bash
# 클라우드가 main에 새로 올린 것을 사람 손 없이 시험한다 (macOS, Linux). Windows는 watch.ps1을 쓴다. 하는 일은 같다.
#
#   몇 분마다 origin/main을 보고, 마지막으로 시험한 뒤 새 커밋(로컬 결과 커밋이 아닌 것)이 있으면
#   setup.sh --commit을 돌린다: 받기, 프로젝트 설정, EditMode·PlayMode 테스트, 화면 캡처, 결과 보고와 푸시.
#   이 컴퓨터에서 Unity가 이 프로젝트를 열고 있거나, 커밋하지 않은 변경이 있으면 그 차례는 건너뛴다.
#
# 사용법: DevTools/Local/watch.sh [--once] [--interval 분] [--install | --uninstall]
#   --once       한 번만 보고 끝낸다(cron에서 쓴다)
#   --interval   몇 분마다 볼지(기본 5)
#   --install    10분마다 --once로 도는 cron 항목을 넣는다. --uninstall은 뺀다
# 마지막으로 시험한 커밋은 Logs/watch-tested.txt, 기록은 Logs/watch.log에 남는다.
set -o pipefail

once=0
interval=5
install=0
uninstall=0
while [ $# -gt 0 ]; do
    case "$1" in
        --once) once=1 ;;
        --interval) interval="$2"; shift ;;
        --install) install=1 ;;
        --uninstall) uninstall=1 ;;
        *) echo "알 수 없는 옵션: $1" >&2; exit 2 ;;
    esac
    shift
done

root="$(cd "$(dirname "$0")/../.." && pwd)"
logs="$root/Logs"
mkdir -p "$logs"
state="$logs/watch-tested.txt"
lock="$logs/watch.lock"
results_subject="Record local Unity setup results"
mark="# pray-to-watch $root"

say() { printf '\033[36m[watch %s] %s\033[0m\n' "$(date '+%H:%M')" "$1"; }
warn() { printf '\033[33m[watch %s] %s\033[0m\n' "$(date '+%H:%M')" "$1" >&2; }

if [ "$install" = 1 ] || [ "$uninstall" = 1 ]; then
    command -v crontab > /dev/null || { warn "crontab이 없습니다. watch.sh를 직접 띄워 두세요."; exit 1; }
    current="$(crontab -l 2> /dev/null | grep -vF "$mark")"
    if [ "$install" = 1 ]; then
        printf '%s\n%s\n' "$current" "*/10 * * * * cd '$root' && bash DevTools/Local/watch.sh --once >> '$logs/watch.log' 2>&1 $mark" | sed '/^$/d' | crontab -
        say "10분마다 새 커밋을 보고 시험하도록 cron에 넣었습니다. 기록: Logs/watch.log"
    else
        printf '%s\n' "$current" | sed '/^$/d' | crontab -
        say "cron에서 뺐습니다."
    fi
    exit 0
fi

# 한 번에 하나만 돈다
if [ -f "$lock" ] && kill -0 "$(cat "$lock")" 2> /dev/null; then
    say "다른 watch가 이미 돌고 있습니다 (PID $(cat "$lock"))."
    exit 0
fi
echo $$ > "$lock"
trap 'rm -f "$lock"' EXIT

# 사람이 Unity로 이 프로젝트를 열고 있는지
unity_open() {
    pgrep -f -- "-[pP]roject[pP]ath $root" > /dev/null 2>&1
}

check() {
    if ! git -C "$root" fetch -q origin main; then
        warn "git fetch가 실패했습니다. 다음 차례에 다시 봅니다."
        return
    fi
    local remote last others
    remote="$(git -C "$root" rev-parse origin/main)"
    last="$(cat "$state" 2> /dev/null)"
    if [ "$remote" = "$last" ]; then
        say "새로 시험할 것이 없습니다."
        return
    fi
    # 그 뒤로 로컬 결과 커밋만 늘었으면 시험할 것이 없다
    if [ -n "$last" ] && git -C "$root" cat-file -e "$last^{commit}" 2> /dev/null; then
        others="$(git -C "$root" log --format=%s "$last..$remote" | grep -vxF "$results_subject" | head -1)"
        if [ -z "$others" ]; then
            echo "$remote" > "$state"
            say "새로 시험할 것이 없습니다."
            return
        fi
    fi
    if [ -n "$(git -C "$root" status --porcelain --untracked-files=no)" ]; then
        warn "커밋하지 않은 변경이 있어 이번 차례는 건너뜁니다(사람이 작업 중일 수 있다)."
        return
    fi
    if unity_open; then
        warn "Unity가 이 프로젝트를 열고 있어 이번 차례는 건너뜁니다."
        return
    fi
    say "새 커밋 $(git -C "$root" log -1 --format='%h %s' origin/main). 시험을 돌립니다."
    bash "$root/DevTools/Local/setup.sh" --commit
    local code=$?
    # 결과 커밋까지 넣은 지금을 기억한다. 실패해도 같은 커밋을 되풀이해 돌리지 않는다.
    git -C "$root" rev-parse HEAD > "$state"
    if [ "$code" = 0 ]; then say "모두 통과. 결과를 올렸습니다."; else warn "실패한 단계가 있습니다. LOCAL_TASKS.md의 결과 보고를 보세요."; fi
}

while :; do
    check
    [ "$once" = 1 ] && break
    sleep $((interval * 60))
done
