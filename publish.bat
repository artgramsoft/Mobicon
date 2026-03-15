@echo off
echo Building Mobicon as a single-file executable...

dotnet publish Mobicon/Mobicon.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ==================================================
    echo Build Successful!
    echo Location: ./publish/Mobicon.exe
    echo ==================================================
) else (
    echo.
    echo Build Failed. Please check the errors above.
)
pause
