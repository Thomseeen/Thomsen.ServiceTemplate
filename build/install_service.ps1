sc.exe create "Thomsen ServiceTemplate" binpath="$(Resolve-Path .\publish\win-x64\ServiceTemplate.exe)"
sc.exe start "Thomsen ServiceTemplate"