using System;
using System.IO;
using System.Linq;

namespace corenes
{
    internal class Cartridge
    {
        public byte[] PRG;
        public byte[] CHR;
        public int _prgrom16kb;
        public int _chrrom8Kb;

        public Cartridge()
        {
            var path = "C:\\GALAXIAN.NES";
            byte[] rom = File.ReadAllBytes(path);
            byte[] header = new ArraySegment<byte>(rom, 0, 16).ToArray();

            // http://wiki.nesdev.com/w/index.php/INES#iNES_file_format
            // First 4 bytes are magic INES header

            _prgrom16kb = header[4];
            _chrrom8Kb = header[5];

            int prgBytes = 0x4000 * _prgrom16kb;
            PRG = new ArraySegment<byte>(rom, 16, prgBytes).ToArray();

            int chrBytes = 0x2000 * _chrrom8Kb;
            CHR = new ArraySegment<byte>(rom, 16 + prgBytes, chrBytes).ToArray();
        }
    }
}