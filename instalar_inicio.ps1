# Crea un acceso directo en la carpeta de Inicio de Windows para que
# MidiShortcuts arranque solo al encender el PC, directo en la bandeja
# del sistema (sin mostrar la ventana). Como la app se va sola a la
# bandeja al cerrar con la X, ya no hace falta el modo --background aparte.
#
# Uso: click derecho -> "Ejecutar con PowerShell" (o correrlo desde una
# terminal de PowerShell) estando en la misma carpeta que MidiShortcuts.exe.

$exePath = Join-Path $PSScriptRoot "MidiShortcuts.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "No se encontro MidiShortcuts.exe en esta carpeta: $exePath" -ForegroundColor Red
    exit 1
}

$startupFolder = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupFolder "MidiShortcuts.lnk"

$WScriptShell = New-Object -ComObject WScript.Shell
$shortcut = $WScriptShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.Arguments = "--minimized"
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.Description = "MIDI Shortcuts - Starrypad mini"
$shortcut.Save()

Write-Host "Listo. Acceso directo creado en: $shortcutPath"
Write-Host "MidiShortcuts.exe arrancara directo en la bandeja del sistema cada vez que inicies sesion en Windows."
Write-Host "Dale doble clic al icono de la bandeja para abrir la ventana cuando quieras."
