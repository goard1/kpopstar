#!/usr/bin/env bash
# Unity 배치 컴파일 (여러 에이전트가 같은 프로젝트를 쓰므로 잠금으로 한 번에 하나만 실행)
# 사용법: bash tools/unity-compile.sh <이름> [executeMethod]
#   예) bash tools/unity-compile.sh visual
#       bash tools/unity-compile.sh content Munbanggu.EditorTools.ProjectSetup.RunBatch
# 결과: "COMPILE OK" 또는 "COMPILE FAILED" + error CS 목록. 로그: Munbanggu/Logs/compile-<이름>.log
set -u
NAME="${1:-agent}"
METHOD="${2:-}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/f/unity/6000.3.29f1/Editor/Unity.exe"
LOCK="$ROOT/.unity-lock"
LOG_WIN="$(cygpath -w "$ROOT/Munbanggu/Logs/compile-$NAME.log")"
LOG="$ROOT/Munbanggu/Logs/compile-$NAME.log"

if tasklist 2>/dev/null | grep -qi "^Unity.exe" && [ ! -d "$LOCK" ]; then
  echo "Unity 에디터가 열려 있어 배치 컴파일을 할 수 없습니다 (사용자가 열었을 수 있음). 나중에 다시 시도하세요."
  exit 2
fi

waited=0
until mkdir "$LOCK" 2>/dev/null; do
  # 20분 넘은 잠금은 비정상 종료로 보고 제거
  if [ -n "$(find "$LOCK" -maxdepth 0 -mmin +20 2>/dev/null)" ]; then rmdir "$LOCK" 2>/dev/null; continue; fi
  [ $waited -eq 0 ] && echo "다른 에이전트가 컴파일 중... 대기 ($(cat "$LOCK/owner" 2>/dev/null))"
  sleep 5; waited=$((waited+5))
done
echo "$NAME" > "$LOCK/owner"
trap 'rm -rf "$LOCK"' EXIT

ARGS=(-batchmode -quit -projectPath "$(cygpath -w "$ROOT/Munbanggu")" -buildTarget Android -logFile "$LOG_WIN")
[ -n "$METHOD" ] && ARGS+=(-executeMethod "$METHOD")
"$UNITY" "${ARGS[@]}" >/dev/null 2>&1
code=$?

errors=$(grep -E "error CS[0-9]+" "$LOG" | sed 's/^.*Assets/Assets/' | sort -u)
exc=$(grep -E "Exception|executeMethod method .* threw" "$LOG" | grep -v "^UnityEngine" | head -20)
if [ -n "$errors" ] || [ $code -ne 0 ]; then
  echo "COMPILE FAILED (exit $code)"
  [ -n "$errors" ] && echo "$errors"
  [ -n "$exc" ] && echo "$exc"
  exit 1
fi
echo "COMPILE OK"
grep -E "warning CS[0-9]+" "$LOG" | grep "_Game" | sed 's/^.*Assets/Assets/' | sort -u | head -20
[ -n "$exc" ] && echo "$exc"
exit 0
