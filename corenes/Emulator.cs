using System;
using System.IO;
using SDL;
using NfdSharp;

namespace corenes
{
    internal enum EmulatorState
    {
        Running,
        Paused,
        Menu
    }

    internal class Emulator
    {
        public Cartridge cartridge;
        public Memory memory;
        public Cpu cpu;
        public Ppu ppu;
        public Apu apu;

        private unsafe SDL_Window* window;
        private unsafe SDL_Renderer* renderer;
        private unsafe SDL_Texture* texture;
        private unsafe SDL_AudioStream* audioStream;

        private const int NES_WIDTH = 256;
        private const int NES_HEIGHT = 240;
        private const int SCALE = 3;

        private EmulatorState _state = EmulatorState.Running;
        private string _currentRomPath;

        // Cached NES palette (RGB565 format) - prevents allocation on every pixel conversion
        private static readonly ushort[] NES_PALETTE_RGB565 = new ushort[64]
        {
            0x7BEF, 0x001F, 0x0017, 0x4017, 0x7817, 0x7810, 0x7000, 0x3800,
            0x0100, 0x0080, 0x0060, 0x0041, 0x0843, 0x0000, 0x0000, 0x0000,
            0xBDF7, 0x039F, 0x181F, 0x801F, 0xB81B, 0xE015, 0xE00C, 0xA800,
            0x4A00, 0x0300, 0x0140, 0x01C2, 0x0247, 0x0000, 0x0000, 0x0000,
            0xFFFF, 0x3DFF, 0x5C9F, 0xBC1F, 0xFC1F, 0xFC18, 0xFBC0, 0xCBC0,
            0x7D40, 0x1E80, 0x0768, 0x0749, 0x076D, 0x4210, 0x0000, 0x0000,
            0xFFFF, 0xAF7F, 0xB65F, 0xE65F, 0xFE5F, 0xFE5C, 0xFE38, 0xF670,
            0xC6A0, 0x8F40, 0x6FCC, 0x5FED, 0x6FF7, 0x9CD6, 0x0000, 0x0000
        };

        public unsafe Emulator(string romPath = null)
        {
            // Initialize SDL
            if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_AUDIO))
            {
                throw new Exception($"SDL_Init failed: {SDL3.SDL_GetError()}");
            }

            // Create window
            window = SDL3.SDL_CreateWindow(
                "CoreNES - NES Emulator",
                NES_WIDTH * SCALE,
                NES_HEIGHT * SCALE,
                0
            );

            if (window == null)
            {
                throw new Exception($"SDL_CreateWindow failed: {SDL3.SDL_GetError()}");
            }

            // Create renderer
            renderer = SDL3.SDL_CreateRenderer(window, null);
            if (renderer == null)
            {
                throw new Exception($"SDL_CreateRenderer failed: {SDL3.SDL_GetError()}");
            }

            // Create texture for NES screen
            texture = SDL3.SDL_CreateTexture(
                renderer,
                SDL_PixelFormat.SDL_PIXELFORMAT_RGB565,
                SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                NES_WIDTH,
                NES_HEIGHT
            );

            if (texture == null)
            {
                throw new Exception($"SDL_CreateTexture failed: {SDL3.SDL_GetError()}");
            }

            // Create audio stream
            SDL_AudioSpec srcSpec = new SDL_AudioSpec
            {
                freq = 44100,
                format = SDL_AudioFormat.SDL_AUDIO_F32,
                channels = 1
            };

            SDL_AudioSpec dstSpec = new SDL_AudioSpec
            {
                freq = 44100,
                format = SDL_AudioFormat.SDL_AUDIO_F32,
                channels = 1
            };

            audioStream = SDL3.SDL_OpenAudioDeviceStream(
                SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK,
                &srcSpec,
                null,
                null
            );

            if (audioStream == null)
            {
                throw new Exception($"SDL_OpenAudioDeviceStream failed: {SDL3.SDL_GetError()}");
            }

            SDL3.SDL_ResumeAudioStreamDevice(audioStream);

            // Initialize emulator components
            _currentRomPath = romPath;
            this.cartridge = new Cartridge(romPath);
            this.memory = new Memory(this, new Mapper0(this.cartridge));
            this.cpu = new Cpu(this);
            this.ppu = new Ppu(this);
            this.apu = new Apu(this.cpu);

            cpu.Reset();
            ppu.Reset();
            apu.Reset();

            Console.WriteLine("CoreNES - NES Emulator");
            Console.WriteLine("Press ESC or F1 to open menu");
            if (!string.IsNullOrEmpty(_currentRomPath))
            {
                Console.WriteLine($"Loaded ROM: {_currentRomPath}");
            }

            // Run emulation loop
            Run();
        }

        private unsafe void Run()
        {
            bool running = true;
            int cpuCycles = 0;
            int ppuCycles = 0;

            while (running)
            {
                // Handle SDL events
                while (SDL3.SDL_PollEvent(out SDL_Event evt))
                {
                    if (evt.type == SDL_EventType.SDL_EVENT_QUIT)
                    {
                        running = false;
                    }
                    else if (evt.type == SDL_EventType.SDL_EVENT_KEY_DOWN)
                    {
                        HandleKeyPress(evt.key.key, ref running);
                    }
                }

                // Only run emulation if not paused
                if (_state == EmulatorState.Running)
                {
                    // Run one CPU instruction
                    cpuCycles = this.cpu.Step();

                    // PPU runs 3 times per CPU cycle
                    ppuCycles = cpuCycles * 3;
                    for (int i = 0; i < ppuCycles; i++)
                    {
                        ppu.Step();
                    }

                    // APU runs once per CPU cycle
                    for (int i = 0; i < cpuCycles; i++)
                    {
                        apu.Step();
                    }

                    // Render frame only when PPU signals a frame is complete (60 FPS)
                    if (ppu.IsFrameReady())
                    {
                        RenderFrame();
                        ppu.ClearFrameReady();

                        // Output audio samples when we render a frame
                        OutputAudio();
                    }
                }
                else if (_state == EmulatorState.Menu)
                {
                    // Render last frame with menu overlay
                    RenderFrame();
                    RenderMenu();
                    SDL3.SDL_RenderPresent(renderer);
                    SDL3.SDL_Delay(16); // ~60 FPS when paused
                }
            }

            Cleanup();
        }

        private unsafe void RenderFrame()
        {
            // Get the front buffer from PPU
            ushort[] frameBuffer = ppu.GetFrameBuffer();

            // Update SDL texture with the frame buffer
            void* pixels;
            int pitch;

            if (SDL3.SDL_LockTexture(texture, null, &pixels, &pitch))
            {
                // Convert palette indices to RGB565 using Span for better performance
                Span<ushort> pixelSpan = new Span<ushort>(pixels, NES_WIDTH * NES_HEIGHT);
                for (int i = 0; i < pixelSpan.Length; i++)
                {
                    // Direct lookup from cached palette (no allocations)
                    pixelSpan[i] = NES_PALETTE_RGB565[frameBuffer[i] & 0x3F];
                }

                SDL3.SDL_UnlockTexture(texture);
            }

            // Clear screen
            SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
            SDL3.SDL_RenderClear(renderer);

            // Render texture
            SDL3.SDL_RenderTexture(renderer, texture, null, null);

            // Present
            SDL3.SDL_RenderPresent(renderer);
        }

        private void HandleKeyPress(SDL_Keycode key, ref bool running)
        {
            if (_state == EmulatorState.Running)
            {
                switch (key)
                {
                    case SDL_Keycode.SDLK_ESCAPE:
                        _state = EmulatorState.Menu;
                        break;
                    case SDL_Keycode.SDLK_F1:
                        _state = EmulatorState.Menu;
                        break;
                }
            }
            else if (_state == EmulatorState.Menu)
            {
                switch (key)
                {
                    case SDL_Keycode.SDLK_ESCAPE:
                    case SDL_Keycode.SDLK_r:
                        _state = EmulatorState.Running;
                        break;
                    case SDL_Keycode.SDLK_o:
                        LoadRomFromDialog();
                        break;
                    case SDL_Keycode.SDLK_q:
                        running = false;
                        break;
                }
            }
        }

        private void LoadRomFromDialog()
        {
            try
            {
                var result = Nfd.FileOpen("nes");
                if (result.Status == NfdStatus.Ok && !string.IsNullOrEmpty(result.Path))
                {
                    // Store current ROM path
                    _currentRomPath = result.Path;

                    // Reload cartridge with new ROM
                    cartridge = new Cartridge(_currentRomPath);
                    memory = new Memory(this, new Mapper0(cartridge));
                    cpu.Reset();
                    ppu.Reset();
                    apu.Reset();

                    // Resume emulation
                    _state = EmulatorState.Running;

                    Console.WriteLine($"Loaded ROM: {_currentRomPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading ROM: {ex.Message}");
            }
        }

        private unsafe void RenderMenu()
        {
            // Draw semi-transparent overlay
            SDL3.SDL_SetRenderDrawBlendMode(renderer, SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 180);
            SDL3.SDL_FRect overlayRect = new SDL_FRect { x = 0, y = 0, w = NES_WIDTH * SCALE, h = NES_HEIGHT * SCALE };
            SDL3.SDL_RenderFillRect(renderer, &overlayRect);

            // Draw menu title
            DrawText("=== MENU ===", 280, 200, 2.0f);
            DrawText("R - Resume", 260, 280, 1.5f);
            DrawText("O - Load ROM", 260, 320, 1.5f);
            DrawText("Q - Quit", 260, 360, 1.5f);

            if (!string.IsNullOrEmpty(_currentRomPath))
            {
                string romName = Path.GetFileName(_currentRomPath);
                DrawText($"Current: {romName}", 200, 450, 1.0f);
            }
        }

        private unsafe void DrawText(string text, int x, int y, float scale)
        {
            // Simple box-based text rendering (each character is a 6x8 box)
            int charWidth = (int)(6 * scale);
            int charHeight = (int)(8 * scale);
            int spacing = (int)(2 * scale);

            for (int i = 0; i < text.Length; i++)
            {
                SDL_FRect charRect = new SDL_FRect
                {
                    x = x + i * (charWidth + spacing),
                    y = y,
                    w = charWidth,
                    h = charHeight
                };

                SDL3.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                SDL3.SDL_RenderFillRect(renderer, &charRect);

                // Draw character outline
                SDL3.SDL_SetRenderDrawColor(renderer, 100, 100, 255, 255);
                SDL3.SDL_RenderRect(renderer, &charRect);
            }
        }

        private unsafe void OutputAudio()
        {
            float[] samples = apu.GetSamples();
            if (samples.Length > 0)
            {
                fixed (float* samplePtr = samples)
                {
                    SDL3.SDL_PutAudioStreamData(audioStream, samplePtr, samples.Length * sizeof(float));
                }
            }
        }

        private unsafe void Cleanup()
        {
            if (audioStream != null)
            {
                SDL3.SDL_DestroyAudioStream(audioStream);
            }

            if (texture != null)
            {
                SDL3.SDL_DestroyTexture(texture);
            }

            if (renderer != null)
            {
                SDL3.SDL_DestroyRenderer(renderer);
            }

            if (window != null)
            {
                SDL3.SDL_DestroyWindow(window);
            }

            SDL3.SDL_Quit();
        }
    }
}