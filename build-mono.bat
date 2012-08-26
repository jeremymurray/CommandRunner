@echo off
setlocal
if not defined gmcs set gmcs=call "c:\Program Files (x86)\Unity\Editor\Data\Mono\bin\gmcs.bat"
%gmcs% -debug+ -d:LINQ -out:NDesk.Options.dll -target:library Options.cs
%gmcs% -debug+ -r:NDesk.Options.dll -r:Newtonsoft.Json.Net35.dll -out:cr.exe CommandRunner.cs
endlocal
