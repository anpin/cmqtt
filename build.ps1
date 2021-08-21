.\Tools\NuGet\nuget.exe install VsIdeBuild -OutputDirectory Tools
.\Tools\VsIdeBuild.1.0.1\tools\VsIdeBuild.exe -Solution CMQTT.sln -BuildSolutionConfiguration Release -Crestron -ShowBuild
if (-not (Test-Path -Path ".\Build\Packages" ))
{
    mkdir ".\Build\Packages"
}
if ((Test-Path -Path ".\Build\Temp" )) {
    rm -Recurse -Force ".\Build\Temp"
}
mkdir ".\Build\Temp"
cp '.\CMQTT\bin\Release\CMQTT.cplz' '.\Build\Temp\'
cp '.\CMQTT\bin\Release\CMQTT.dll' '.\Build\Temp\'
cp '.\CMQTT.nuspec' '.\Build\Temp\'
cp '.\LICENSE.txt' '.\Build\Temp'
.\Tools\NuGet\NuGet.exe pack .\Build\Temp\CMQTT.nuspec -OutputDirectory ".\Build\Packages"
