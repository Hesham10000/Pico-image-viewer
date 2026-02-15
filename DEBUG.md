## Find errors

````shell
& "C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe" `
   -batchmode -quit `
   -projectPath "C:\Users\hesha\projects\Pico-image-viewer" `
   -logFile - | Tee-Object unity-errors.log

````

````shell

Select-String -Path .\unity-full-errors.log -Pattern "error|exception|failed" -CaseSensitive:$false


````