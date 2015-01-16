@echo off
for /r %%x in (packages.config) do (
	if exist "%%x" %~dp0\.nuget\nuget.exe install "%%x" -OutputDirectory "%~dp0\Packages" -SolutionDirectory "%~dp0"
)
