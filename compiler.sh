#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/PhantomHaze.csproj"
TEMP_ROOT="${PHANTOMHAZE_TEMP_ROOT:-$SCRIPT_DIR/tmp}"
OUTPUT_DIR="${1:-$TEMP_ROOT/PhantomHaze/publish}"
LOG_DIR="$TEMP_ROOT/PhantomHaze/logs"
BIN_DIR="$TEMP_ROOT/PhantomHaze/bin"
OBJ_DIR="$TEMP_ROOT/PhantomHaze/obj"
EXE_PATH="$OUTPUT_DIR/PhantomHaze.exe"
LOG_FILE="$LOG_DIR/compiler-$(date -u +%Y%m%dT%H%M%SZ).log"

mkdir -p "$LOG_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$OBJ_DIR"
touch "$LOG_FILE"

print_log_links_on_exit() {
  local exit_code="$1"

  if [[ "$exit_code" -eq 0 ]]; then
    echo "Build complete: $EXE_PATH"
  else
    echo "Build failed with exit code $exit_code."
  fi

  echo "Compiled EXE path: $EXE_PATH"
  echo "Compiled EXE URI: file://$EXE_PATH"
  echo "Compiler log: $LOG_FILE"
  echo "Compiler log URI: file://$LOG_FILE"
  echo "Runtime logs directory: $LOG_DIR"
  echo "Runtime logs URI: file://$LOG_DIR"
}

on_exit() {
  local exit_code=$?
  print_log_links_on_exit "$exit_code"
}

trap on_exit EXIT
exec > >(tee -a "$LOG_FILE") 2>&1

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet SDK is not installed or not on PATH." >&2
  exit 1
fi

echo "Using .NET SDK $(dotnet --version)"
echo "Publishing $PROJECT_FILE to $OUTPUT_DIR"
echo "Temp build paths: bin=$BIN_DIR obj=$OBJ_DIR"

dotnet publish "$PROJECT_FILE" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableWindowsTargeting=true \
  -p:BaseOutputPath="$BIN_DIR/" \
  -p:BaseIntermediateOutputPath="$OBJ_DIR/" \
  -o "$OUTPUT_DIR"