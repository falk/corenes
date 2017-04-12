using System;
using System.Collections.Generic;
using System.Text;

namespace corenes
{
    class Memory
    {
        private byte[] RAM;
        private IMapper mapper;

        public Memory(IMapper mapper)
        {
            this.mapper = mapper;
            RAM = new byte[0x2000];
        }

        public byte read(ushort address)
        {
            if (address < 0x2000)
            {
                return RAM[address & 0x7FF];
            }
            else if (address < 0x4000)
            {
                // read ppu (picture)
            }
            else if (address == 0x4014)
            {
                // read ppu (picture)
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
                return this.mapper.read(address);
            }
            return new byte();
        }

        public void write(ushort address, byte value)
        {
             if (address < 0x2000)
            {
                RAM[address & 0x7FF] = value;
            }
            else if (address < 0x4000)
            {
                // read ppu (picture)
            }
            else if (address == 0x4014)
            {
                // read ppu (picture)
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
    }
}