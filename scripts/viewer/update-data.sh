#!/usr/bin/env bash
set -e
if [ -z "$1" ]; then
    echo "Usage: update-data.sh <path/to/manifest.json>" >&2
    exit 1
fi
SRC="$1"
if [ ! -f "$SRC" ]; then
    echo "Error: file not found: $SRC" >&2
    exit 1
fi
DST="src-viewer/wwwroot/data/manifest.json"
python3 - "$SRC" "$DST" <<'EOF'
import json, sys
KEEP = {
    'Deployment','Set','TextLabel','QueryLabel','TimestampUtc',
    'Status','DurationMs','RetryCount','Model',
    'PromptTokens','CompletionTokens','TotalTokens',
    'ContentSha256Short','Error','ListTask','OrderTask',
    'ExactMatches','Coverage','Hallucinations','TitleHit','TextbookHit',
    'SectionCount','Index','Li1First','PositionScore','MinMoves','OrderPct'
}
with open(sys.argv[1]) as f:
    records = json.load(f)
stripped = [{k: v for k, v in r.items() if k in KEEP} for r in records]
with open(sys.argv[2], 'w') as f:
    json.dump(stripped, f, separators=(',',':'))
print(f"{len(records)} records -> {len(stripped)} stripped, written to {sys.argv[2]}")
EOF
