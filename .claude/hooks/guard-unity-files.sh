#!/usr/bin/env bash
# Cloud sessions do not edit what the Unity editor owns (CLAUDE.md 작업 분담, Docs/Research2026-11.md 후보 6): scenes,
# prefabs, assets, .meta files, ProjectSettings/ and Packages/packages-lock.json. Writing them by hand corrupts
# Unity's IDs; the local session makes them through editor scripts. Only in cloud sessions (CLAUDE_CODE_REMOTE),
# so a local session is not stopped. Moving or deleting a .meta beside a file stays allowed (git mv / git rm).
[ "$CLAUDE_CODE_REMOTE" = "true" ] || exit 0
input=$(cat)
path=$(printf '%s' "$input" | grep -o '"\(file_path\|notebook_path\)"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*:[[:space:]]*"\(.*\)"/\1/')
[ -n "$path" ] || exit 0
case "$path" in
  *.unity|*.prefab|*.asset|*.meta|*.mat|*.anim|*.controller|*.overrideController|*.physicMaterial|*/ProjectSettings/*|*/Packages/packages-lock.json)
    echo "클라우드 세션은 Unity가 관리하는 파일($path)을 직접 고치지 않습니다. 에디터 스크립트(ProjectSetup.SetUp)로 만들고 LOCAL_TASKS.md에 로컬 할 일을 적으세요(CLAUDE.md 작업 분담)." >&2
    exit 2
    ;;
esac
exit 0
