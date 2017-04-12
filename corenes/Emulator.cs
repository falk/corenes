namespace corenes
{
    internal class Emulator
    {
        private Cartridge cartridge;
        private Memory memory;
        private Cpu cpu;

        public Emulator()
        {
            this.cartridge = new Cartridge();
            this.memory = new Memory(new Mapper0(this.cartridge));
            this.cpu = new Cpu(this.memory);
            cpu.Reset();

            var cpuCycles = 0;

            while (true)
            {
                cpuCycles += this.cpu.Step();
            }
        }
    }
}