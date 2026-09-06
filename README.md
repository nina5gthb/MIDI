# MIDI Shortcuts - Starrypad mini (version C# / WinForms)

Migracion 1:1 del programa en Python a C#, usando la interfaz grafica
nativa de Windows (WinForms). Compatible con Windows 10 y 11.

**Sin dependencias externas ni NuGet**: el MIDI se maneja directo con la
API de Windows (`winmm.dll`) y el envio de teclas con `SendInput`, ambos
via P/Invoke. Solo necesitas el SDK de .NET, nada mas que instalar.

## Requisitos para compilar

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Al instalar el SDK, asegurate de tener el "workload" de escritorio de
  Windows (Windows Desktop). El instalador oficial de Visual Studio o el
  instalador standalone del SDK ya lo incluyen normalmente.

## Compilar y correr

Desde una terminal, parado en esta carpeta (donde esta `MidiShortcuts.csproj`):

```
dotnet build -c Release
```

El ejecutable queda en `bin\Release\net8.0-windows\MidiShortcuts.exe`.

Para generar un .exe listo para copiar a otra PC sin instalar el SDK ahi:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Eso te deja un solo `MidiShortcuts.exe` en
`bin\Release\net8.0-windows\win-x64\publish\` que corre sin depender de
tener .NET instalado en la maquina destino.

## Uso

- **Interfaz grafica:** doble clic en `MidiShortcuts.exe`, o `MidiShortcuts.exe` desde la terminal.
- **Segundo plano (sin ventana):** `MidiShortcuts.exe --background`
- **Inicio automatico con Windows:** corre `instalar_inicio.ps1` (crea un
  acceso directo en la carpeta de Inicio que lanza el modo `--background`).

## Compatibilidad con tu config.json existente

El archivo `config.json` usa exactamente el mismo formato que la version
en Python (`port_name`, `mappings` con `type`/`value`). Si ya tenias
atajos configurados, solo copia tu `config.json` a la carpeta del nuevo
`.exe` y se leen igual, sin tener que rehacerlos.

## Que se porto igual que la version Python

- Deteccion y eleccion automatica de puerto MIDI (prioriza nombres con
  "star"/"pad", evitando los que digan "daw").
- Arranque automatico de la escucha al abrir la app.
- Reconexion automatica si el dispositivo se desconecta (util con
  Bluetooth/BLE-MIDI, que puede cortarse y reconectar solo), con un
  margen de 15s entre reintentos fallidos para no saturar con popups.
- Boton "Nuevo atajo" con captura en vivo del pad/knob.
- Tipos de accion: tecla/combinacion, escribir texto, abrir programa.
- Log a archivo (`midi_shortcuts.log`) junto al .exe.
- Modo segundo plano que le cede el paso a la GUI si la abres despues (y
  viceversa), para no pelearse por el puerto MIDI.

## Diferencias a tener en cuenta

- El envio de teclas usa la API nativa de Windows (`SendInput`) en vez de
  la libreria `keyboard` de Python. Funciona igual para la mayoria de
  combinaciones, pero **no puede enviar teclas a una ventana que corre
  como Administrador** a menos que `MidiShortcuts.exe` tambien corra como
  Administrador (esto es una restriccion de Windows, UIPI, y tambien
  aplicaba igual con `keyboard` en Python).
- El bloqueo de otra instancia usa una señal interna de Windows en vez de
  matar el proceso con PowerShell; es mas limpio pero solo funciona entre
  instancias de este mismo programa.
