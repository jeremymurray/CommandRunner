@echo off
setlocal
if not defined csc set csc="C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"
set compile_flags=/nologo /debug /optimize /define:debug;LINQ
set lib_flags=/target:library

set compile_cmd=%csc% %compile_flags%
set lib_cmd=%csc% %compile_flags% %lib_flags%

%lib_cmd% /out:NDesk.Options.dll Options.cs
%compile_cmd% /r:NDesk.Options.dll /r:Newtonsoft.Json.Net35.dll /out:cr.exe CommandRunner.cs
endlocal
