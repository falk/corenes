using System;
using System.IO;
using System.Linq;

namespace corenes
{
    internal class Cartridge
    {
        public byte[] PRG;
        public byte[] CHR;
        public Cartridge()
        {
            var path = "C:\\GALAXIAN.NES";
            byte[] rom = File.ReadAllBytes(path);
            byte[] header = new ArraySegment<byte>(rom, 0, 16).ToArray();

            // http://wiki.nesdev.com/w/index.php/INES#iNES_file_format
            // First 4 bytes are magic INES header

            var PRGROM_16KB = header[4];
            var CHRROM_8KB = header[5];

            int prgBytes = 0x4000 * PRGROM_16KB;
            PRG = new ArraySegment<byte>(rom, 16, prgBytes).ToArray();

            int chrBytes = 0x2000 * CHRROM_8KB;
            CHR = new ArraySegment<byte>(rom, 16 + prgBytes, chrBytes).ToArray();
        }
    }
}