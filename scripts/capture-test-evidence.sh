#!/usr/bin/env bash

set -o pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

run_id="$(date -u +"%Y%m%dT%H%M%SZ")"
output_dir="logs/test-evidence/$run_id"
mkdir -p "$output_dir"

capture_state() {
  local output_file="$1"
  {
    git rev-parse HEAD
    git status --porcelain=v1
  } >"$output_file"
}

run_step() {
  local name="$1"
  shift

  printf '$'
  printf ' %q' "$@"
  printf '\n'
  "$@" 2>&1 | tee "$output_dir/$name.log"
  local pipeline_status=("${PIPESTATUS[@]}")
  local command_status=${pipeline_status[0]}
  local tee_status=${pipeline_status[1]}
  printf '\ncommand_exit_code=%s\ntee_exit_code=%s\n' "$command_status" "$tee_status" >>"$output_dir/$name.log"

  if [[ "$command_status" -ne 0 ]]; then
    return "$command_status"
  fi

  return "$tee_status"
}

capture_state "$output_dir/pre-state.txt"
{
  printf 'project=gitic\n'
  printf 'started_utc=%s\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  printf 'dotnet_sdk=%s\n' "$(dotnet --version)"
  printf 'test_project=tests/Gitic.Tests/Gitic.Tests.csproj\n'
} >"$output_dir/metadata.txt"

overall_status=0
if ! run_step restore dotnet restore tests/Gitic.Tests/Gitic.Tests.csproj --nologo; then
  overall_status=1
fi

if ! run_step release-build dotnet build Gitic.csproj --configuration Release --no-restore --nologo; then
  overall_status=1
fi

if ! run_step unit-tests dotnet test tests/Gitic.Tests/Gitic.Tests.csproj --no-restore --nologo --logger "console;verbosity=normal"; then
  overall_status=1
fi

capture_state "$output_dir/post-state.txt"
if cmp -s "$output_dir/pre-state.txt" "$output_dir/post-state.txt"; then
  worktree_state="unchanged"
else
  worktree_state="changed"
  diff -u "$output_dir/pre-state.txt" "$output_dir/post-state.txt" >"$output_dir/state-diff.txt" || true
  overall_status=1
fi

{
  printf '# Gitic test evidence\n\n'
  printf -- '- **Run ID:** `%s`\n' "$run_id"
  printf -- '- **Overall exit code:** `%s`\n' "$overall_status"
  printf -- '- **Worktree state:** `%s`\n\n' "$worktree_state"
  printf 'Raw output: `restore.log`, `release-build.log`, and `unit-tests.log`.\n'
} >"$output_dir/summary.md"

printf 'Evidence written to %s\n' "$output_dir"
exit "$overall_status"
