#!/usr/bin/env bash
# Chạy Cafe.EditorTools.CafeMenu.SetupAll ở batch mode, lặp lại tới khi chạy trọn
# (Unity có thể reload domain giữa chừng khi import nhiều asset mới -> hàm bị ngắt mà exit 0).
# Dùng:  bash tools/unity_setup.sh [--build]
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe}"
PROJECT="$ROOT/game"
LOG="$PROJECT/Logs/batch_setup.log"

for attempt in 1 2 3; do
  "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod Cafe.EditorTools.CafeMenu.SetupAll -logFile "$LOG" >/dev/null 2>&1
  code=$?
  if grep -q "error CS" "$LOG"; then
    echo "LỖI BIÊN DỊCH:"; grep "error CS" "$LOG" | sort -u; exit 1
  fi
  if grep -q "\[Cafe\] Setup All xong" "$LOG"; then
    echo "Setup xong (lần $attempt)."; break
  fi
  echo "Setup bị ngắt (lần $attempt, exit $code), chạy lại..."
  [ "$attempt" = 3 ] && { echo "Setup thất bại sau 3 lần, xem $LOG"; exit 1; }
done

if [ "${1:-}" = "--build" ]; then
  BLOG="$PROJECT/Logs/batch_build.log"
  "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod Cafe.EditorTools.BuildTools.BuildWindowsTest -logFile "$BLOG" >/dev/null 2>&1
  grep -E "error CS|\[Cafe\] Build" "$BLOG" | sort -u
fi
