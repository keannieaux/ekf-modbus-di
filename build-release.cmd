@echo off
rem =====================================================
rem  Сборка релизной версии ShnoSetting для заказчика
rem  Результат: dist\ShnoSetting.zip (ShnoSetting.exe + profiles)
rem =====================================================
cd /d "%~dp0"

echo [1/4] Очистка старой сборки...
if exist dist rmdir /s /q dist

echo [2/4] Публикация (Release, win-x64, один файл)...
dotnet publish AvaloniaApplication1\AvaloniaApplication1.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\ShnoSetting
if errorlevel 1 (
    echo.
    echo ОШИБКА СБОРКИ. Если файл занят - закройте запущенное приложение.
    pause
    exit /b 1
)

echo [3/4] Переименование и чистка лишнего...
cd dist\ShnoSetting
del /q *.pdb
ren AvaloniaApplication1.exe ShnoSetting.exe
cd ..\..

echo [4/4] Упаковка в zip...
powershell -NoProfile -Command "Compress-Archive -Path 'dist\ShnoSetting' -DestinationPath 'dist\ShnoSetting.zip' -Force"

echo.
echo ГОТОВО: dist\ShnoSetting.zip
pause
