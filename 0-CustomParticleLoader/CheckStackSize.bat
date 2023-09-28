echo off
set size=4194304
set client=7DaysToDie.exe
set dedi=7DaysToDieServer.exe
if exist ..\..\%client% (
    .\editbin.exe /stack:%size% ..\..\%client%
    echo Setting max stack size for %client% to %size%
) ^
else if exist ..\..\%dedi% (
    .\editbin.exe /stack:%size% ..\..\%dedi%
    echo Setting max stack size for %dedi% to %size%
) ^
else (
    echo 7 days to die executable not found!
)
pause