#!/usr/bin/env bash
# Generate SQL scripts for EF Core migrations for both DbContexts
# Usage: ./generate-migration-sql.sh [--output dir] [--idempotent] [--what-if]

set -euo pipefail

OUTPUT_DIR="migrations-sql"
IDEMPOTENT=true
WHAT_IF=false

print_usage() {
  cat <<EOF
Usage: $0 [--output dir] [--idempotent] [--what-if] [--help]

Options:
  --output DIR      Output directory (default: $OUTPUT_DIR)
#   --no-idempotent   Generate non-idempotent scripts (default: idempotent)
  --what-if         Dry-run: show commands but do not execute
  --help            Show this help

Examples:
  $0 --output sql --idempotent
  $0 --what-if
EOF
}

# parse args
while [[ $# -gt 0 ]]; do
  case $1 in
    --output) OUTPUT_DIR="$2"; shift 2 ;;
    --no-idempotent) IDEMPOTENT=false; shift 1 ;;
    --what-if) WHAT_IF=true; shift 1 ;;
    -h|--help) print_usage; exit 0 ;;
    *) echo "Unknown arg: $1"; print_usage; exit 1 ;;
  esac
done

IDEMPOTENT_FLAG=""
if [ "$IDEMPOTENT" = true ]; then
  IDEMPOTENT_FLAG="--idempotent"
fi

if [ "$WHAT_IF" = true ]; then
  echo "WhatIf mode: commands will not be executed"
else
  mkdir -p "$OUTPUT_DIR"
fi

# JohodpDbContext
JOH_OUT="$OUTPUT_DIR/migration-johodp.sql"
CMD1=(dotnet ef migrations script $IDEMPOTENT_FLAG -p src/Johodp.Infrastructure -s src/Johodp.Api --context JohodpDbContext --output "$JOH_OUT")
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: ${CMD1[*]}"
else
  echo "Generating JohodpDbContext migration script -> $JOH_OUT"
  "${CMD1[@]}"
fi

# PersistedGrantDbContext
IDSVR_OUT="$OUTPUT_DIR/migration-identityserver.sql"
CMD2=(dotnet ef migrations script $IDEMPOTENT_FLAG -p src/Johodp.Infrastructure -s src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext --output "$IDSVR_OUT")
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: ${CMD2[*]}"
else
  echo "Generating PersistedGrantDbContext migration script -> $IDSVR_OUT"
  "${CMD2[@]}"
fi

echo "All scripts placed in: $OUTPUT_DIR"