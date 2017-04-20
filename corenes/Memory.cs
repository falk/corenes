using System;
using System.Collections.Generic;
using System.Text;

namespace corenes
{
    class Memory
    {
        private byte[] RAM;
        private IMapper mapper;
        private Emulator emulator;

        public Memory(Emulator emulator, IMapper mapper)
        {
            this.mapper = mapper;
            RAM = new byte[0x2000];
            this.emulator = emulator;
        }

        public byte Read(ushort address)
        {
            if (address < 0x2000)
            {
                return RAM[address % 0x0800];
            }
            if (address < 0x4000)
            {
                return emulator.ppu.ReadRegister((ushort) (0x2000 + (address % 8)));
            }
            if (address == 0x4014)
            {
                return emulator.ppu.ReadRegister(address);
            }
            if (address == 0x4015)
            {
                // read apu (audio)
            }
            if (address == 0x4016)
            {
                // read controller 1
            }
            if (address >= 0x6000)
            {
                return mapper.read(address);
            }
            return new byte();
        }

        public byte ReadPpu(ushort address)
        {
            address = (ushort)(address % 0x4000);
            if (address < 0x2000)
            {
                return mapper.read(address);
            }
            if (address < 0x3F00)
            {
                var mode = emulator.cartridge.Mirroring;
                return emulator.ppu.nameTableData[MirrorAddress(mode, address) % 2048];
            }
            if (address < 0x4000)
            {
                return emulator.ppu.ReadPalette((ushort) (address % 32));
            }
            return 0;
        }

        public void WritePPU(ushort address, byte value)
        {
            address = (ushort)(address % 0x4000);
            if (address < 0x2000)
            {
                mapper.write(address, value);
            }
            if (address < 0x3F00)
            {
                var mode = emulator.cartridge.Mirroring;
                emulator.ppu.nameTableData[MirrorAddress(mode, address) % 2048] = value;
            }
            if (address < 0x4000)
            {
                emulator.ppu.WritePalette((ushort)(address % 32), value);
            }
        }

        public void Write(ushort address, byte value)
        {
             if (address < 0x2000)
            {
                RAM[address % 0x0800] = value;
            }
            else if (address < 0x4000)
            {
                emulator.ppu.WriteRegister((ushort) (0x2000 + address % 8), value);
            }
            else if (address == 0x4014)
            {
                emulator.ppu.WriteRegister(address, value);
            }
            else if (address == 0x4015)
            {
                // read apu (picture)
            }
            else if (address == 0x4016)
            {
                // read controller 1
            } else if (address >= 0x6000)
            {
                this.mapper.write(address, value);
            }
           
        }

        private ushort MirrorAddress(MirrorMode mode, ushort address)
        {
            int[,] mirrorLookup =
            {
                {0, 0, 1, 1},
                {0, 1, 0, 1},
                {0, 0, 0, 0},
                {1, 1, 1, 1},
                {0, 1, 2, 3},
            };

            address = (ushort) ((address - 0x2000) % 0x1000);
            var table = address / 0x0400;
            var offset = address % 0x0400;
            return (ushort) (0x2000 + mirrorLookup[(int) mode, table] * 0x0400 + offset);
        }
}
}