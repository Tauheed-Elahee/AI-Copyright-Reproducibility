@echo off
:: Usage: status.bat <project-dir>
::        For live refresh: run in a loop — e.g. watch.bat, or Task Scheduler
:: The HARNESS_LOG env var overrides automatic log discovery.
setlocal
set "PROJECT=%~1"
if "%PROJECT%"=="" (
    echo Usage: status.bat ^<project-dir^> >&2
    exit /b 1
)

powershell -NoProfile -Command ^
    "$project = '%PROJECT%';" ^
    "$logDir  = if ($env:HARNESS_LOG) { $null } else { Join-Path $project 'log' };" ^
    "$output  = if ($env:HARNESS_LOG) { $env:HARNESS_LOG } else {" ^
    "    Get-ChildItem $logDir -Filter 'harness-*.log' -ErrorAction SilentlyContinue ^|" ^
    "    Sort-Object LastWriteTime -Descending ^| Select-Object -First 1 -ExpandProperty FullName" ^
    "};" ^
    "Write-Host '╔══════════════════════════════════════════════════════╗' -ForegroundColor Cyan;" ^
    "Write-Host '║         AI-Copyright-Reproducibility Monitor         ║' -ForegroundColor Cyan;" ^
    "Write-Host '╚══════════════════════════════════════════════════════╝' -ForegroundColor Cyan;" ^
    "Write-Host ('Updated: ' + (Get-Date -Format 'HH:mm:ss')) -ForegroundColor DarkGray;" ^
    "Write-Host '';" ^
    "if (-not $output -or -not (Test-Path $output)) {" ^
    "    Write-Host ('Waiting for log file in ' + $logDir + ' ...') -ForegroundColor DarkGray; exit" ^
    "};" ^
    "Write-Host ('Log: ' + (Split-Path $output -Leaf)) -ForegroundColor DarkGray;" ^
    "Write-Host '';" ^
    "$lines   = Get-Content $output;" ^
    "$runPat  = '^\[.*\] set=\d+/\d+ rep=';" ^
    "$sets    = ($lines ^| Select-String 'Sets\s*:\s*(\d+)'    ^| Select-Object -First 1).Matches.Groups[1].Value;" ^
    "$reps    = ($lines ^| Select-String 'Reps/set:\s*(\d+)'   ^| Select-Object -First 1).Matches.Groups[1].Value;" ^
    "$deplLn  = ($lines ^| Select-String '^Deployments:'       ^| Select-Object -First 1).Line;" ^
    "$nDepl   = if ($deplLn) { ($deplLn -replace 'Deployments:\s*','').Split(',').Count } else { 0 };" ^
    "$nPr     = ($lines ^| Select-String 'Bound prompts: (\d+)' ^| Select-Object -First 1).Matches.Groups[1].Value;" ^
    "$total   = if ($sets -and $reps -and $nDepl -gt 0 -and $nPr) { [int]$sets*[int]$reps*$nDepl*[int]$nPr } else { 0 };" ^
    "$runLns  = $lines ^| Select-String $runPat;" ^
    "$lastRun = ($runLns ^| Select-Object -Last 1).Line;" ^
    "if ($lastRun) {" ^
    "    $dep = [regex]::Match($lastRun,'^\[([^\]]+)\]').Groups[1].Value.TrimEnd();" ^
    "    $sc  = [regex]::Match($lastRun,'set=(\d+)/').Groups[1].Value;" ^
    "    $st  = [regex]::Match($lastRun,'set=\d+/(\d+)').Groups[1].Value;" ^
    "    $rc  = [regex]::Match($lastRun,'rep=\s*(\d+)/').Groups[1].Value;" ^
    "    $rt  = [regex]::Match($lastRun,'rep=\s*\d+/(\d+)').Groups[1].Value;" ^
    "    $sta = [regex]::Match($lastRun,'status=(\d+)').Groups[1].Value;" ^
    "    $dur = [regex]::Match($lastRun,'(\d+)ms').Groups[1].Value;" ^
    "    $qry = [regex]::Match($lastRun,'query=(\S+)').Groups[1].Value;" ^
    "    $sem = [regex]::Match($lastRun,'sem=(\S+)').Groups[1].Value;" ^
    "    $out = [regex]::Match($lastRun,'out=(\S+)').Groups[1].Value;" ^
    "    $sc2 = if ($sta -eq '200') { 'Green' } else { 'Red' };" ^
    "    Write-Host 'Latest:  ' -NoNewline -ForegroundColor White;" ^
    "    Write-Host $dep.PadRight(22) -NoNewline -ForegroundColor Cyan;" ^
    "    Write-Host ('  set='+$sc+'/'+$st+'  rep='+$rc+'/'+$rt);" ^
    "    Write-Host ('  query='+$qry.PadRight(26)+'  status=') -NoNewline;" ^
    "    Write-Host $sta -NoNewline -ForegroundColor $sc2;" ^
    "    Write-Host ('  '+$dur+'ms  out='+$out+'  sem='+$sem);" ^
    "    Write-Host '';" ^
    "    $done = $runLns.Count;" ^
    "    if ($total -gt 0) {" ^
    "        $pct = [math]::Min(100,[int]($done*100/$total));" ^
    "        $fl  = [int]($pct*30/100); $em = 30-$fl;" ^
    "        $bar = ([string][char]0x2588)*$fl + ([string][char]0x2591)*$em;" ^
    "        Write-Host 'Progress: [' -NoNewline;" ^
    "        Write-Host $bar -NoNewline -ForegroundColor Green;" ^
    "        Write-Host ('] '+$pct+'%  ('+$done+' / '+$total+' runs)')" ^
    "    } else { Write-Host ('Progress: '+$done+' runs recorded') }" ^
    "} else { Write-Host 'No runs started yet...' -ForegroundColor DarkGray };" ^
    "$errs = ($lines ^| Select-String 'ERROR:').Count;" ^
    "if ($errs -gt 0) {" ^
    "    Write-Host '';" ^
    "    Write-Host ('Errors seen: '+$errs) -ForegroundColor Red;" ^
    "    $lines ^| Select-String 'ERROR:' ^| Select-Object -Last 3 ^| ForEach-Object {" ^
    "        Write-Host ('  '+$_.Line) -ForegroundColor Red }" ^
    "}"
