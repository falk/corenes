using System;
using Microsoft.Xna.Framework;

namespace corenes
{
    internal class Emulator : Game
    {
        public Cartridge cartridge;
        public Memory memory;
        public Cpu cpu;
        public Ppu ppu;

        public Emulator()
        {
            this.cartridge = new Cartridge();
            this.memory = new Memory(this, new Mapper0(this.cartridge));
            this.cpu = new Cpu(this);
            this.ppu = new Ppu(this);
            
            cpu.Reset();

            int cpuCycles = 0;
            int ppuCycles = 0;

            while (true)
            {
                cpuCycles = this.cpu.Step();
                // PPU runs 3 times per cpu cycle
                ppuCycles = cpuCycles * 3;
                for (int i = 0; i < ppuCycles; i++)
                {
                    ppu.Step();
                }
 //               Console.WriteLine(cpuCycles);
            }
        }
    }
}