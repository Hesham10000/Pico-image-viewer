## Find errors

````shell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" `
   -batchmode -quit `
   -projectPath "C:\Users\hesha\projects\Pico-image-viewer" `
   -logFile - | Tee-Object unity-full-errors.log

````

````shell

Select-String -Path .\unity-full-errors.log -Pattern "error|exception|failed" -CaseSensitive:$false | Tee-Object unity-errors.log


````

## run emulator
````shell
C:\sparrow_rls_oversea_K_pico_emulator_win64_20250924\sparrow_rls_oversea_K_pico_emulator_win64_20250924\picoemulator\emulator.exe
````

## list devices to see emulator
````shell
C:\platform-tools-latest-windows\platform-tools\adb.exe devices
C:\platform-tools-latest-windows\platform-tools\adb.exe install -r "C:\Users\hesha\projects\Pico-Starter\pico-starter.apk"
C:\platform-tools-latest-windows\platform-tools\adb.exe install -r "C:\Users\hesha\projects\Pico-image-viewer\img-viewer.apk"


````

## Debug

````shell
C:\platform-tools-latest-windows\platform-tools\adb.exe shell pm list packages | findstr -i viewer
C:\platform-tools-latest-windows\platform-tools\adb.exe shell pm list packages | findstr /i xleven
C:\platform-tools-latest-windows\platform-tools\adb.exe shell cmd package resolve-activity --brief Xleven.PicoImageViewer
C:\platform-tools-latest-windows\platform-tools\adb.exe logcat -c
C:\platform-tools-latest-windows\platform-tools\adb.exe shell am start -n Xleven.PicoImageViewer/.MainActivity
C:\platform-tools-latest-windows\platform-tools\adb.exe logcat -d | findstr /i "FATAL EXCEPTION AndroidRuntime ActivityManager Unity libc Xleven PicoImageViewer"



````


## Obtain headset log
https://developer.picoxr.com/zh/document/unreal/pdc-faq/
````shell
adb pull data/logs 
````


