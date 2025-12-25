using System;

namespace corenes
{
    class Program
    {
        static void Main(string[] args)
        {
            string romPath = null;

            // Check if ROM path provided via command line
            if (args.Length > 0)
            {
                romPath = args[0];
                Console.WriteLine($"Loading ROM from command line: {romPath}");
            }

            try
            {
                Emulator emulator = new Emulator(romPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("\nUsage:");
                Console.WriteLine("  dotnet run                    - Load default ROM (mario.NES)");
                Console.WriteLine("  dotnet run <path-to-rom>      - Load specific ROM file");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}