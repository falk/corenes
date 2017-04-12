namespace corenes
{
    internal interface IMapper
    {
        byte read(ushort address);
        void write(ushort address, byte value);
    }
}