@echo off
setlocal
echo Building Mobicon (Ultra Light Weight)...

:: 기존 폴더 삭제
if exist publish rmdir /s /q publish

:: 가장 단순하고 가벼운 기본 빌드 (Portable/Framework-Dependent)
dotnet publish Mobicon/Mobicon.csproj -c Release -o ./publish

:: 빌드 성공 여부 확인
if %ERRORLEVEL% NEQ 0 goto :failed

:success
echo.
echo ==================================================
echo Build Successful! (Ultra Light)
echo.
echo [Files generated in ./publish]
echo - Mobicon.exe (App Loader)
echo - Mobicon.dll (App Core)
echo ==================================================
goto :end

:failed
echo.
echo ==================================================
echo Build Failed. Please check the errors above.
echo ==================================================

:end
pause
endlocal
