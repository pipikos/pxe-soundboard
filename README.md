PXE SoundBoard (Windows)

A fast, reliable soundboard for Windows built with .NET 8, WinForms, and NAudio.

Grid of pads with labels, colors, per-pad volume, modes (Cut/Overlap)

Right-click a pad to open a full Pad Settings window (choose file, edit label/color, set volume/mode, set a hotkey)

Master volume, Stop All, and Output device selection (e.g., route to VoiceMeeter Input/AUX/VAIO3)

Layout controls: Rows/Cols, Font size, Padding

Configuration stored in config.json, with auto-save and auto-reload (debounced file watcher)

⚠️ Hotkeys are focus-based (work while the app window is focused). Global background hotkeys are not enabled in this build.

Table of Contents

System Requirements

Download & Run (No Installer — ZIP)

First Run & Basic Usage

Add Sounds to Pads

Route Audio to VoiceMeeter

Configuration File (config.json)

Build From Source

Create a Portable Release ZIP

Troubleshooting

FAQ

License

System Requirements

OS: Windows 10/11 (x64)

Runtime:

If you use a self-contained release ZIP: No .NET install needed.

If you build framework-dependent: install .NET 8 Runtime/SDK.

Audio: Standard Windows audio stack (WASAPI). For routing/mixing, VoiceMeeter is supported via selecting its virtual inputs as the output device.

Supported audio formats (via NAudio + Windows Media Foundation): WAV, MP3, AIFF (and often WMA/M4A if Media Foundation is present). On Windows N editions, install the Media Feature Pack for full codec support.

Download & Run (No Installer — ZIP)

Recommended for users who just want to run the app.

Download the release ZIP from this repository’s Releases page:
PXE-SoundBoard-vX.Y.Z-win-x64.zip

Extract the ZIP to a folder you control (e.g., C:\Apps\PXE-SoundBoard).

Double-click SoundBoard.exe to run.

The app will create or load config.json next to the EXE.

SmartScreen may warn about unsigned executables. Click More info → Run anyway.

First Run & Basic Usage
Top Bar

Volume: master volume (0–100%)

Stop All: stops all currently playing sounds

Output: choose an audio output device (WASAPI). Select VoiceMeeter Input/AUX/VAIO3 to route into VoiceMeeter.

Rows / Cols: change the grid size (number of pads visible)

Font / Padding: adjust pad label font size and outer padding

Apply Grid: apply layout changes immediately (saved to config.json)

Reload Config: reloads config.json on demand (auto-reload also runs when the file changes)

Pads

Left-click a pad → play it

Right-click a pad → open Pad Settings to edit:

Text (Label)

Audio File (file picker)

Mode:

Cut → stops previous instance of that pad before playing

Overlap → allows layered playback of the pad

Per-Pad Volume (0.0–1.0)

Color (#RRGGBB)

Hotkey (works while the app has focus)

Changes are saved immediately to config.json and the UI updates automatically.

Add Sounds to Pads

Right-click a pad → Pad Settings.

Click … and choose an audio file (WAV/MP3/AIFF/etc.).

Optionally set Label, Color, Mode, and Per-Pad Volume.

Click OK. The pad updates and your settings are saved.

Tip: Keep your audio files in a stable path (e.g., C:\Sounds\…). If you move files, update the pad’s file path.

Route Audio to VoiceMeeter

Install and open VoiceMeeter (Banana/Potato).

In PXE SoundBoard, pick Output → select VoiceMeeter Input, VoiceMeeter AUX Input, or VoiceMeeter VAIO3 Input (depending on your setup).

In VoiceMeeter, you’ll see the soundboard arriving on the corresponding virtual input channel. Mix/route as needed.

If you don’t see VoiceMeeter devices, ensure VoiceMeeter is installed and its virtual devices are enabled, then restart PXE SoundBoard.

Configuration File (config.json)

The app reads/writes a config.json next to SoundBoard.exe. Example:

{
  "GridRows": 4,
  "GridCols": 4,
  "ButtonFontSize": 12,
  "ButtonPadding": 6,
  "Pads": [
    {
      "Label": "Intro",
      "FilePath": "C:\\\\Sounds\\\\intro.wav",
      "Hotkey": "1",
      "Mode": "Cut",
      "Volume": 1.0,
      "Color": "#3b82f6"
    },
    {
      "Label": "Clap",
      "FilePath": "C:\\\\Sounds\\\\clap.mp3",
      "Hotkey": "2",
      "Mode": "Overlap",
      "Volume": 0.9,
      "Color": "#16a34a"
    }
  ],
  "SelectedOutputDeviceId": null
}


GridRows / GridCols — grid size

ButtonFontSize — pad label font size

ButtonPadding — visual padding around buttons

Pads[] — per-pad settings (the app auto-extends/shrinks this list to match the grid)

SelectedOutputDeviceId — persisted WASAPI device ID (updated when you change Output in the UI)

You can hand-edit config.json (e.g., with VS Code) while the app is running. The app auto-reloads with a short debounce to avoid loops.

Build From Source
Prerequisites

.NET 8 SDK
Install from Microsoft or via package manager (on Windows you can use winget install Microsoft.DotNet.SDK.8).

Git (optional, for cloning)

NAudio is referenced via NuGet in the project file

Clone & Run
git clone https://github.com/<your-username>/pxe-soundboard.git
cd pxe-soundboard
dotnet restore
dotnet run

Project File (SoundBoard.csproj)

The project targets net8.0-windows, enables WinForms, and references NAudio:

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NAudio" Version="2.2.1" />
  </ItemGroup>

  <ItemGroup>
    <None Update="config.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>

Create a Portable Release ZIP

Produce a self-contained build (no .NET installation required on the target PC):

cd C:\SoundBoard
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:IncludeNativeLibrariesForSelfExtract=true


Package the output:

$ver = "v0.1.0"
$out = "bin\Release\net8.0-windows\win-x64\publish"
Compress-Archive -Path "$out\*" -DestinationPath "PXE-SoundBoard-$ver-win-x64.zip" -Force


Distribute the ZIP. Users just extract & run SoundBoard.exe.

We keep PublishSingleFile=false so that config.json stays external and easy to edit/watch.

Troubleshooting

App doesn’t see changes in config.json.

The app auto-reloads config with a debounce. If editing in a tool that creates temp files, click Reload Config in the app after saving.

Selected output device isn’t used.

Go to Output dropdown in the app and re-select the desired device (e.g., VoiceMeeter Input). The choice is saved to config.json.

No sound or format errors (MP3/M4A/WMA).

On Windows N editions, install the Media Feature Pack to enable Media Foundation decoders.

Try with a WAV to verify the chain.

“Timer is ambiguous between System.Windows.Forms.Timer and System.Threading.Timer”.

This happens only if you modify the code and change usings. In our code we disambiguate timers via aliases (using WinFormsTimer = System.Windows.Forms.Timer; using ThreadingTimer = System.Threading.Timer;). Keep those lines or fully qualify the types.

Build error about NAudio not found.

Ensure the <PackageReference Include="NAudio" Version="2.2.1" /> exists and run dotnet restore.

Crashes or “Playback error”.

Verify the file path exists and is readable.

Try different latency (we use 80 ms) or different output device.

FAQ

Does it support global hotkeys?
Not in this build. Hotkeys work when the app has focus. Global hotkeys may be added in a future version.

Can I use MIDI to trigger pads?
Not in this build. MIDI mapping is on the roadmap.

How do I reset everything?
Close the app and delete config.json next to SoundBoard.exe. Reopen the app; a new default config will be created.

How do I move my setup to another PC?
Copy the entire app folder, including config.json and your audio files.

License

This project is released under the MIT License. See the LICENSE file for details.

Need help?

Open an Issue with:

App version (ZIP/release tag)

Windows version

Your audio output selection (e.g., VoiceMeeter Input/AUX/VAIO3)

What you tried and any screenshots/logs

We’re happy to help you get rolling!
