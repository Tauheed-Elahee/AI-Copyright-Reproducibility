#!/bin/bash
# Usage: status.sh <project-dir>
#        watch -n 2 -c bash ./scripts/view/status.sh <project-dir>

if [ -z "$1" ]; then
    echo "Usage: status.sh <project-dir>" >&2
    exit 1
fi

PROJECT="$1"
LOG_DIR="$PROJECT/log"

OUTPUT=${AICR_LOG:-$(ls -t "$LOG_DIR"/aicr-*.log 2>/dev/null | head -1)}

BOLD='\033[1m'
CYAN='\033[1;36m'
GREEN='\033[1;32m'
YELLOW='\033[1;33m'
RED='\033[1;31m'
DIM='\033[2m'
RESET='\033[0m'

printf "${CYAN}╔══════════════════════════════════════════════════════╗${RESET}\n"
printf "${CYAN}║         AI-Copyright-Reproducibility Monitor         ║${RESET}\n"
printf "${CYAN}╚══════════════════════════════════════════════════════╝${RESET}\n"
printf "${DIM}Updated: $(date '+%H:%M:%S')${RESET}\n\n"

if [ -z "$OUTPUT" ] || [ ! -f "$OUTPUT" ]; then
    printf "  ${DIM}Waiting for log file in %s ...${RESET}\n" "$LOG_DIR"
    exit 0
fi

printf "${DIM}Log: %s${RESET}\n\n" "$(basename "$OUTPUT")"

# Run completion line pattern:
# [deployment           ] set=S/T rep= R/T text=...   query=...   status=NNN  DDDms out=N sem=HASH
RUN_PAT='^\[.*\] set=[0-9]+/[0-9]+ rep='

# --- Extract run totals from log header ---
SETS=$(grep -oP 'Sets\s*:\s*\K[0-9]+' "$OUTPUT" | head -1)
REPS=$(grep -oP 'Reps/set:\s*\K[0-9]+' "$OUTPUT" | head -1)
N_DEPL=$(grep '^Deployments:' "$OUTPUT" | head -1 | grep -oP 'Deployments:\s*\K.*' | tr ',' '\n' | grep -c '\S')
N_PROMPTS=$(grep 'Bound prompts:' "$OUTPUT" | head -1 | grep -oP 'Bound prompts: \K[0-9]+')

TOTAL_EXPECTED=0
if [ -n "$SETS" ] && [ -n "$REPS" ] && [ "$N_DEPL" -gt 0 ] && [ -n "$N_PROMPTS" ]; then
    TOTAL_EXPECTED=$((SETS * REPS * N_DEPL * N_PROMPTS))
fi

# --- Latest run line ---
LAST_RUN=$(grep -E "$RUN_PAT" "$OUTPUT" | tail -1)
if [ -n "$LAST_RUN" ]; then
    DEPLOYMENT=$(echo "$LAST_RUN" | grep -oP '^\[\K[^\]]+' | sed 's/[[:space:]]*$//')
    SET_CUR=$(echo "$LAST_RUN" | grep -oP 'set=\K[0-9]+(?=/)')
    SET_TOT=$(echo "$LAST_RUN" | grep -oP 'set=[0-9]+/\K[0-9]+')
    REP_CUR=$(echo "$LAST_RUN" | grep -oP 'rep=\s*\K[0-9]+(?=/)')
    REP_TOT=$(echo "$LAST_RUN" | grep -oP 'rep=\s*[0-9]+/\K[0-9]+')
    STATUS=$(echo "$LAST_RUN"  | grep -oP 'status=\K[0-9]+')
    DUR=$(echo "$LAST_RUN"     | grep -oP '[0-9]+(?=ms)')
    OUT=$(echo "$LAST_RUN"     | grep -oP 'out=\K\S+')
    SEM=$(echo "$LAST_RUN"     | grep -oP 'sem=\K\S+')
    QUERY=$(echo "$LAST_RUN"   | grep -oP 'query=\K\S+')

    if [ "$STATUS" = "200" ]; then STATUS_COLOR=$GREEN; else STATUS_COLOR=$RED; fi

    printf "${BOLD}Latest:${RESET}  ${CYAN}%-22s${RESET}  set=%s/%s  rep=%s/%s\n" \
        "$DEPLOYMENT" "$SET_CUR" "${SET_TOT:-?}" "$REP_CUR" "${REP_TOT:-?}"
    printf "  query=%-26s  status=${STATUS_COLOR}%s${RESET}  %sms  out=%s  sem=%s\n\n" \
        "$QUERY" "$STATUS" "$DUR" "$OUT" "$SEM"

    # Progress bar
    COMPLETED=$(grep -cE "$RUN_PAT" "$OUTPUT" 2>/dev/null || echo 0)
    if [ "$TOTAL_EXPECTED" -gt 0 ]; then
        PCT=$((COMPLETED * 100 / TOTAL_EXPECTED))
        [ "$PCT" -gt 100 ] && PCT=100
        FILLED=$((PCT * 30 / 100))
        EMPTY=$((30 - FILLED))
        BAR=""
        [ "$FILLED" -gt 0 ] && BAR=$(printf '%0.s█' $(seq 1 $FILLED))
        [ "$EMPTY"  -gt 0 ] && BAR="${BAR}$(printf '%0.s░' $(seq 1 $EMPTY))"
        printf "Progress: [${GREEN}%s${RESET}] %d%%  (%d / %d runs)\n" \
            "$BAR" "$PCT" "$COMPLETED" "$TOTAL_EXPECTED"
    else
        printf "Progress: %d runs recorded\n" "$COMPLETED"
    fi
else
    printf "${DIM}No runs started yet...${RESET}\n"
fi

# --- Sleeping between sets ---
LAST_RESUME=$(grep -n "Resuming at" "$OUTPUT" | tail -1)
if [ -n "$LAST_RESUME" ]; then
    LAST_RESUME_NUM=$(echo "$LAST_RESUME" | cut -d: -f1)
    LAST_RUN_NUM=$(grep -nE "$RUN_PAT" "$OUTPUT" | tail -1 | cut -d: -f1)
    if [ "$LAST_RESUME_NUM" -gt "${LAST_RUN_NUM:-0}" ]; then
        RESUME_TIME=$(echo "$LAST_RESUME" | grep -oP 'Resuming at \K[0-9:]+')
        printf "\n${YELLOW}  ⏳ Sleeping between sets — resuming at %s UTC${RESET}\n" "$RESUME_TIME"
    fi
fi

# --- Identity groups from last completed run ---
LAST_GROUPS_LINE=$(grep -n "Distinct semantic hashes" "$OUTPUT" | tail -1 | cut -d: -f1)
if [ -n "$LAST_GROUPS_LINE" ]; then
    printf "\n${BOLD}Last completed run — identity groups:${RESET}\n"
    tail -n +"$LAST_GROUPS_LINE" "$OUTPUT" | head -12 | while IFS= read -r line; do
        if echo "$line" | grep -q "group 1:"; then
            printf "  ${GREEN}%s${RESET}\n" "$line"
        else
            printf "  %s\n" "$line"
        fi
    done
fi

# --- Error summary ---
ERRORS=$(grep -cP "ERROR:" "$OUTPUT" 2>/dev/null || echo 0)
if [ "$ERRORS" -gt 0 ]; then
    printf "\n${RED}${BOLD}Errors seen: %d${RESET}\n" "$ERRORS"
    grep "ERROR:" "$OUTPUT" | tail -3 | while IFS= read -r line; do
        printf "  ${RED}%s${RESET}\n" "$line"
    done
fi
