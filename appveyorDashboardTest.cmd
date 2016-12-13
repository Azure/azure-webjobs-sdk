vstest.console /logger:Appveyor /TestAdapterPath:bin bin/Dashboard.EndToEndTests.dll
set testexit=%errorlevel%
REM appveyor hangs if child processes are running & testeasy fails to properly dispose iis processes
taskkill /IM iis*
exit %testexit%