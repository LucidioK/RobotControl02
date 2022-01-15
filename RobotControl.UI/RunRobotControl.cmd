IF EXIST "bin\Release\net5.0-windows\RobotControl.UI.exe" goto :Start
pushd .
cd ..
dotnet build -c Release
popd
:Start
pushd .
IF NOT EXIST "bin\Release\net5.0-windows\RobotControl.UI.exe" goto :CannotFind
cd bin\Release\net5.0-windows 
start /REALTIME RobotControl.UI.exe
popd
goto :End
:CannotFind
@echo off
echo [91m
echo .
echo .
echo .
echo Could not find bin\Release\net5.0-windows\RobotControl.UI.exe
echo You should be executing this script on the same directory
echo of RobotControl.UI.csproj
echo .
echo .[0m
pause
:End


