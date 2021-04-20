IF EXIST "bin\Release\net5.0-windows\RobotControl.UI.exe" goto :Start
pushd .
cd ..
dotnet build -c Release
popd
:Start
pushd .
cd bin\Release\net5.0-windows 
start /REALTIME RobotControl.UI.exe
popd

