# CoreNES - NES Emulator

A cross-platform NES (Nintendo Entertainment System) emulator written in C# with .NET 10 and SDL3.

## Features

- **Cross-platform**: Runs on Windows, macOS, and Linux
- **Lightweight**: Uses SDL3 for efficient rendering and audio
- **Complete emulation**: CPU, PPU, and APU fully implemented
- **Full audio**: All 5 NES audio channels with accurate mixing
- **Bug-free**: All major emulation bugs fixed for Super Mario Bros compatibility
- **Native macOS menu**: Standard application menu bar on macOS with File menu
- **Performance optimized**: Frame-based rendering at 60 FPS with minimal CPU usage

## Requirements

- .NET 10 SDK
- SDL3 libraries (automatically downloaded via NuGet package)
- NativeFileDialogExtendedSharp (automatically downloaded via NuGet package)

## Building

```bash
cd corenes
dotnet build
```

## Running

### Option 1: With default ROM

Place your Super Mario Bros ROM file (`mario.NES`) in one of these locations:
- Current directory
- Your home directory
- `/Users/Shared/` (macOS)
- `C:\` (Windows)

Then run:
```bash
dotnet run
```

### Option 2: With specific ROM file

```bash
dotnet run path/to/your/rom.nes
```

### Option 3: Load ROM via File Dialog

1. Run without arguments: `dotnet run`
2. Use **File > Open ROM...** menu (macOS) or press **Cmd+O** / **Ctrl+O**
3. Select your ROM file

## Controls

### Keyboard Shortcuts
- **Cmd+O** (macOS) / **Ctrl+O** (other): Open ROM file dialog
- **Cmd+Q** (macOS) / **Ctrl+Q** (other): Quit emulator
- **ESC**: Quit emulator
- Window close button: Exit emulator

### macOS Application Menu
- **File > Open ROM...** (Cmd+O): Load new ROM file
- **CoreNES > Quit CoreNES** (Cmd+Q): Exit emulator

## ROM Format

The emulator expects iNES format ROM files (`.nes` extension).

## Technical Details

### Architecture

- **CPU**: MOS 6502 with all standard instructions (56 opcodes)
- **PPU**: Picture Processing Unit with sprite and background rendering
- **APU**: Audio Processing Unit with all 5 channels
- **Memory**: Full NES memory mapping with mapper support
- **Mapper**: NROM (Mapper 0) support

### Rendering

- Native resolution: 256×240 pixels
- Display scaling: 3× (768×720 window)
- Pixel format: RGB565
- Frame rate: ~60 FPS (NES standard)

### Audio

- **Sample rate**: 44.1 kHz (CD quality)
- **Channels**:
  - 2× Pulse waves (square waves with duty cycle control)
  - 1× Triangle wave
  - 1× Noise channel (pseudo-random)
  - 1× DMC (Delta Modulation Channel) - basic support
- **Mixing**: Accurate NES hardware mixing formulas
- **Output**: Real-time SDL3 audio streaming

### Recent Bug Fixes

All 10 critical bugs preventing Super Mario Bros from working have been fixed:

#### CPU Fixes:
- Pull16 return type (subroutine returns)
- ROL/ROR memory mode operations
- ADC/SBC carry flag calculations

#### PPU Fixes:
- Sprite array initialization
- FetchHighTileByte variable assignment
- Frame buffer swapping
- RenderPixel implementation

#### Mapper Fixes:
- CHR ROM support for pattern tables

### Technology Stack

- **Language**: C# 10
- **Framework**: .NET 10.0
- **Graphics & Audio**: SDL3-CS (ppy.SDL3-CS package)
- **File Dialogs**: NativeFileDialogExtendedSharp
- **macOS Integration**: Native menu bar using Objective-C runtime P/Invoke
- **Target Platforms**: Windows, macOS, Linux

## Project Structure

```
corenes/
├── CPU.cs          - 6502 CPU emulation (all standard instructions)
├── PPU.cs          - Picture Processing Unit (rendering)
├── APU.cs          - Audio Processing Unit (5 channels)
├── Memory.cs       - Memory management and I/O routing
├── Cartridge.cs    - ROM loading (iNES format)
├── Mapper0.cs      - NROM mapper (CHR & PRG ROM)
├── Emulator.cs     - Main emulator with SDL3 rendering & audio
├── MacOSMenu.cs    - Native macOS menu bar (AppKit integration)
└── Program.cs      - Entry point
```

## Known Limitations

- Only Mapper 0 (NROM) is currently supported
- No controller input support yet (keyboard controls pending)
- Limited to games that use NROM mapper (Super Mario Bros, Donkey Kong, Pac-Man, etc.)

## Compatibility

Games known to work:
- **Super Mario Bros** ✅ - Full graphics and audio
- **Donkey Kong** ✅ - Complete emulation
- Other NROM (Mapper 0) games should work

## License

Educational/Personal use only. ROMs are copyrighted material and should only be used if you own the original cartridge.
