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

            int offset;
            if (cartridge._prgrom16kb == 1 && address >= 0xC000)
            {
                offset = 0xC000;
            }
            else
            {
                offset = 0x8000;
            }

            int add = address - offset;
            // @todo Support upper bank
            return cartridge.PRG[add];
        }

        public void write(ushort address, byte value)
        {
            throw new Exception("Mapper 0 does not support writes");
        }
    }
}
