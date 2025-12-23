using System;
using System.IO;
using System.Linq;

namespace corenes
{
    public enum MirrorMode
    {
        Vertical,
        Horizontal
    }

    internal class Cartridge
    {
        public byte[] PRG;
        public byte[] CHR;
        public int _prgrom16kb;
        public int _chrrom8Kb;

        public MirrorMode Mirroring{ get; set; }

        public Cartridge()
        {
            // Try to find mario.NES in multiple locations (cross-platform)
            string[] possiblePaths = new[]
            {
                "mario.NES",                                    // Current directory
                Path.Combine(Directory.GetCurrentDirectory(), "mario.NES"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mario.NES"),
                "/Users/Shared/mario.NES",                     // macOS common location
                "C:\\mario.NES"                                 // Windows location
            };

            string path = null;
            foreach (var possiblePath in possiblePaths)
            {
                if (File.Exists(possiblePath))
                {
                    path = possiblePath;
                    break;
                }
            }

            if (path == null)
            {
                throw new FileNotFoundException(
                    "Could not find mario.NES. Please place the ROM file in one of these locations:\n" +
                    "  - Current directory\n" +
                    "  - Your home directory\n" +
                    "  - /Users/Shared/ (macOS)\n" +
                    "  - C:\\ (Windows)");
            }

            byte[] rom = File.ReadAllBytes(path);
            byte[] header = new ArraySegment<byte>(rom, 0, 16).ToArray();

            // http://wiki.nesdev.com/w/index.php/INES#iNES_file_format
            // First 4 bytes are magic INES header

            _prgrom16kb = header[4];
            _chrrom8Kb = header[5];

            Mirroring = (header[6] & 1) == 1 ? MirrorMode.Vertical : MirrorMode.Horizontal;

            int prgBytes = 0x4000 * _prgrom16kb;
            PRG = new ArraySegment<byte>(rom, 16, prgBytes).ToArray();

            int chrBytes = 0x2000 * _chrrom8Kb;
            CHR = new ArraySegment<byte>(rom, 16 + prgBytes, chrBytes).ToArray();
        }
    }
}