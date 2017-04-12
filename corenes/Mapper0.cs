using System;
using System.Collections.Generic;
using System.Text;


// NROM

namespace corenes
{
    class Mapper0 : IMapper
    {
        private Cartridge cartridge;

        public Mapper0(Cartridge cartridge)
        {
            this.cartridge = cartridge;
        }

        public byte read(ushort address)
        {
            // @todo Support upper bank
            int offset = address - 0x8000;
            return cartridge.PRG[offset];
        }

        public void write(ushort address, byte value)
        {
            int offset = address - 0x8000;
            cartridge.PRG[offset] = value;
        }
    }
}
