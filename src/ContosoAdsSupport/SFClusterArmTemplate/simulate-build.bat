@echo off
REM This script assumes that:
REM - You have the Azure Artifacts Credential Provider installed
REM - You have the Git client installed
REM - You have the Git Credential Manager installed
REM - You have Nuget Installed (use the command `winget install Microsoft.NuGet` to install)
@echo on

set "initialDir=%cd%"

cd /d %~dp0

cd ..

@echo off
REM This brings in the Service Fabric build tools for later
@echo on

nuget restore

cd ContosoSupport

dotnet clean ContosoSupport.csproj
dotnet publish ContosoSupport.csproj /p:Configuration=Release /p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml

cd ..
cd ContosoSupport.API.SF.GE

dotnet msbuild ContosoSupport.API.SF.GE.sfproj /p:Configuration=Release /t:clean
dotnet msbuild ContosoSupport.API.SF.GE.sfproj /p:Configuration=Release /t:package

cd pkg\Release

setlocal enabledelayedexpansion

for %%F in ("..\..") do set "ParentFolderName=%%~nxF"

for /f %%a in ('powershell -command "Get-Date -Format yyyyMMddHHmm"') do set timestamp=%%a

set "outputFileName=%ParentFolderName%.%timestamp%"
powershell -command "Compress-Archive -Path * -DestinationPath '%outputFileName%.zip'"

copy %outputFileName%.zip ..\..\..\SFClusterArmTemplate\Artifacts\%outputFileName%.sfpkg

git add -f ..\..\..\SFClusterArmTemplate\Artifacts\%outputFileName%.sfpkg

del %outputFileName%.zip

cd ..\..\..\SFClusterArmTemplate\Parameters

for /f "tokens=*" %%i in ('powershell -Command "(Get-Content 'App.parameters.json' | ConvertFrom-Json).parameters.applicationPackageUrl.value"') do set curPackageName=%%i

set escapedPackageName=%curPackageName:\=\\%

powershell -Command "(Get-Content 'App.parameters.json' -Raw).Replace('%escapedPackageName%', 'Artifacts\\%ParentFolderName%.%timestamp%.sfpkg') | Set-Content 'App.parameters.json'"

git add -f App.parameters.json

endlocal

cd /d %initialDir%