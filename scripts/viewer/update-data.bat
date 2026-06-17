@echo off
if "%~1"=="" (
    echo Usage: update-data.bat ^<path\to\manifest.json^> >&2
    exit /b 1
)
if not exist "%~1" (
    echo Error: file not found: %~1 >&2
    exit /b 1
)
python -c "import json,sys; KEEP={'Deployment','Set','TextLabel','QueryLabel','TimestampUtc','Status','DurationMs','RetryCount','Model','PromptTokens','CompletionTokens','TotalTokens','ContentSha256Short','Error','ListTask','OrderTask','ExactMatches','Coverage','Hallucinations','TitleHit','TextbookHit','SectionCount','Index','Li1First','PositionScore','MinMoves','OrderPct'}; f=open(sys.argv[1]); records=json.load(f); f.close(); stripped=[{k:v for k,v in r.items() if k in KEEP} for r in records]; f=open(sys.argv[2],'w'); json.dump(stripped,f,separators=(',',':')); f.close(); print(f'{len(records)} records -> {len(stripped)} stripped, written to {sys.argv[2]}')" "%~1" "src-viewer\wwwroot\data\manifest.json"
