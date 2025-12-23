# CoreNES - NES Emulator

A cross-platform NES (Nintendo Entertainment System) emulator written in C# with .NET 10 and SDL3.

## Features

- **Cross-platform**: Runs on Windows, macOS, and Linux
- **Lightweight**: Uses SDL3 for efficient rendering
- **Fixed bugs**: All major emulation bugs fixed for Super Mario Bros compatibility

## Requirements

- .NET 10 SDK
- SDL3 libraries (automatically downloaded via NuGet package)

## Building

```bash
cd corenes
dotnet build
```

## Running

1. Place your Super Mario Bros ROM file (`mario.NES`) in one of these locations:
   - Current directory
   - Your home directory
   - `/Users/Shared/` (macOS)
   - `C:\` (Windows)

2. Run the emulator:
```bash
dotnet run
```

## Controls

- **ESC**: Exit emulator
- Window close button: Exit emulator

## ROM Format

The emulator expects iNES format ROM files (`.nes` extension).

## Technical Details

### Architecture

- **CPU**: MOS 6502 with all standard instructions
- **PPU**: Picture Processing Unit with sprite and background rendering
- **Memory**: Full NES memory mapping with mapper support
- **Mapper**: NROM (Mapper 0) support

### Rendering

- Native resolution: 256×240 pixels
- Display scaling: 3× (768×720 window)
- Pixel format: RGB565
- Frame rate: ~60 FPS (NES standard)

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
- **Graphics**: SDL3-CS (ppy.SDL3-CS package)
- **Target Platforms**: Windows, macOS, Linux

## Project Structure

```
corenes/
├── CPU.cs          - 6502 CPU emulation
├── PPU.cs          - Picture Processing Unit
├── Memory.cs       - Memory management
├── Cartridge.cs    - ROM loading
├── Mapper0.cs      - NROM mapper
├── Emulator.cs     - Main emulator with SDL rendering
└── Program.cs      - Entry point
```

## Known Limitations

- Only Mapper 0 (NROM) is currently supported
- No audio emulation yet
- No controller input support yet
- Limited to games that use NROM mapper (Super Mario Bros, Donkey Kong, etc.)

## License

Educational/Personal use only. ROMs are copyrighted material and should only be used if you own the original cartridge.
