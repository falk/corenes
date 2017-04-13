using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace corenes
{
    class Ppu
    {
        private Memory _memory;
        private Cpu _cpu;

        private ulong _frame;

        private ushort[] _imageFront = new ushort[256 * 240];
        private ushort[] _imageBack = new ushort[256 * 240];

        // PPU registers
        private ushort _v; // current vram address (15 bit)

        private ushort _t; // temporary vram address (15 bit)
        private byte _x; // fine x scroll (3 bit)
        private byte _w; // write toggle (1 bit)
        private byte _f; // even/odd frame flag (1 bit)
        private byte _nmiDelay;
        private bool _nmiOutput;
        private bool _nmiOccurred;
        private int _showBackground;
        private int _showSprites;
        private int _scanline;
        private int _cycle;
        private int _spriteOverflow;
        private int _spriteZeroHit;
        private bool _nmiPrevious;
        private int _spriteCount;
        private int _spriteSize;
        private byte[] _oamData;
        private int[] _spritePatterns;
        private byte[] _spritePositions;
        private byte[] _spritePriorities;
        private byte[] _spriteIndices;

        // $2000 PPUCTRL
        private byte _flagNameTable; // 0: $2000; 1: $2400; 2: $2800; 3: $2C00

        private byte _flagIncrement; // 0: add 1; 1: add 32
        private byte _flagSpriteTable; // 0: $0000; 1: $1000; ignored in 8x16 mode
        private byte _flagBackgroundTable; // 0: $0000; 1: $1000
        private byte _flagSpriteSize; // 0: 8x8; 1: 8x16
        private byte _flagMasterSlave; // 0: read EXT; 1: write EXT


        // Background temporary variables
        private byte _nameTableByte;

        private byte _attributeTableByte;
        private byte _lowTileByte;
        private byte _highTileByte;
        private int _tileData;
        private Cpu cpu;
        private byte _register;
        private byte _oamAddress;

        public Ppu(Emulator emulator)
        {
            _cpu = emulator.cpu;
            _memory = emulator.memory;
        }


        public void Step()
        {
            Tick();

            bool renderingEnabled = _showBackground != 0 || _showSprites != 0;
            bool preLine = _scanline == 261;
            bool visibleLine = _scanline < 240;
            bool renderLine = preLine || visibleLine;
            bool preFetchCycle = _cycle >= 321 && _cycle <= 336;
            bool visibleCycle = _cycle >= 1 && _cycle <= 256;
            bool fetchCycle = preFetchCycle || visibleCycle;

            if (renderingEnabled)
            {
                if (visibleLine && visibleCycle)
                {
                   RenderPixel();
                }
                if (renderLine && fetchCycle)
                {
                    _tileData <<= 4;

                    switch (_cycle % 8)
                    {
                        case 1:
                            FetchNameTableByte();
                            break;
                        case 3:
                            FetchAttributeTable();
                            break;
                        case 5:
                            FetchLowTileByte();
                            break;
                        case 7:
                            FetchHighTileByte();
                            break;
                        case 0:
                            StoreTileData();
                            break;
                    }
                }
                if (preLine && _cycle >= 280 && _cycle <= 304)
                {
                    CopyY();
                }

                if (renderLine)
                {
                    if (fetchCycle && _cycle % 8 == 0)
                    {
                        IncrementX();
                    }
                    if (_cycle == 256)
                    {
                        IncrementY();
                    }
                    if (_cycle == 257)
                    {
                        CopyX();
                    }
                }
            }

            if (renderingEnabled)
            {
                if (_cycle == 257)
                {
                    if (visibleLine)
                    {
                        EvaluateSprites();
                    }
                    else
                    {
                        _spriteCount = 0;
                    }
                }
            }

            if (_scanline == 241 && _cycle == 1)
            {
                SetVBlank();
            }

            if (preLine && _cycle == 1)
            {
                SetVBlank();
                _spriteZeroHit = 0;
                _spriteOverflow = 0;
            }
        }

        private void RenderPixel()
        {
            throw new NotImplementedException();
        }

        private void CopyX()
        {
            _v = (ushort) ((_v & 0xFBE0) | (_t & 0x041F));
        }

        private void CopyY()
        {
            _v = (ushort) ((_v & 0x841F) | (_t & 0x7BE0));
        }


        private void StoreTileData()
        {
            int data = 0;
            for (int i = 0; i < 8; i++)
            {
                var a = _attributeTableByte;
                var p1 = (_lowTileByte & 0x80) >> 7;
                var p2 = (_highTileByte & 0x80) >> 6;
                _lowTileByte <<= 1;
                _highTileByte <<= 1;
                data <<= 4;
                data |= a | p1 | p2;
            }
            _tileData |= data;
        }

        private void FetchHighTileByte()
        {
            var fineY = (_v >> 12) & 7;
            var table = _flagBackgroundTable;
            var tile = _nameTableByte;
            var address = 0x1000 * table + tile * 16 + fineY;
            _lowTileByte = _memory.read((ushort) (address + 8));
            ;
        }

        private void FetchLowTileByte()
        {
            var fineY = (_v >> 12) & 7;
            var table = _flagBackgroundTable;
            var tile = _nameTableByte;
            var address = 0x1000 * table + tile * 16 + fineY;
            _lowTileByte = _memory.read((ushort) address);
        }

        private void FetchAttributeTable()
        {
            var v = _v;
            ushort address = (ushort) (0x23C0 | (v & 0x0C00) | ((v >> 4) & 0x38) | ((v >> 2) & 0x07));
            var shift = ((v >> 4) & 4) | (v & 2);
            _attributeTableByte = (byte) (((_memory.read(address) >> shift) & 3) << 2);
        }

        private void FetchNameTableByte()
        {
            var v = _v;
            ushort address = (ushort) (0x2000 | (v & 0x0FFF));
            _nameTableByte = _memory.read(address);
        }

        private void EvaluateSprites()
        {
            int height = (_spriteSize == 0) ? 8 : 16;

            int count = 0;
            for (int i = 0; i < 64; i++)
            {
                byte y = _oamData[i * 4 + 0];
                byte a = _oamData[i * 4 + 2];
                byte x = _oamData[i * 4 + 3];

                var row = _scanline - y;

                if (row < 0 || row >= height)
                {
                    continue;
                }

                if (count < 8)
                {
                 //   _spritePatterns[count] = fetchSpritePattern(i, row);
                    _spritePositions[count] = x;
                    _spritePriorities[count] = (byte) ((a >> 5) & 1);
                    _spriteIndices[count] = (byte) i;
                }
                count++;
            }
            if (count > 8)
            {
                count = 8;
                _spriteOverflow = 1;
            }
            _spriteCount = count;
        }

        private void SetVBlank()
        {
            _imageFront = _imageBack;
            _imageBack = _imageFront;
            _nmiOccurred = true;
            NmiChange();
        }

        private void NmiChange()
        {
            var nmi = _nmiOutput && _nmiOccurred;
            if (nmi && !_nmiPrevious)
            {
                // TODO: this fixes some games but the delay shouldn't have to be so
                // long, so the timings are off somewhere
                _nmiDelay = 15;
            }
            _nmiPrevious = nmi;
        }

        private void Tick()
        {
            if (_nmiDelay > 0)
            {
                _nmiDelay--;
                if (_nmiDelay == 0 && _nmiOutput && _nmiOccurred)
                {
                    _cpu.TriggerNMI();
                }
            }

            if (_showBackground != 0 && _showSprites != 0)
            {
                if (_f == 1 && _scanline == 261 && _cycle == 339)
                {
                    _cycle = 0;
                    _scanline = 0;
                    _frame++;
                    _f ^= 1;
                    return;
                }
            }

            _cycle++;
            if (_cycle > 340)
            {
                _cycle = 0;
                _scanline++;
                if (_scanline > 261)
                {
                    _scanline = 0;
                    _frame++;
                    _f ^= 1;
                }
            }
        }

        private void IncrementY()
        {
            // increment vert(v)
            // if fine Y < 7
            if ((_v & 0x7000) != 0x7000) {
                // increment fine Y
                _v += 0x1000;
            }
            else
            {
                // fine Y = 0
                _v &= 0x8FFF;
                // let y = coarse Y
                var y = (_v & 0x03E0) >> 5;
                if (y == 29) {
                    // coarse Y = 0
                    y = 0;
                    // switch vertical nametable
                    _v ^= 0x0800;
                }
                else if (y == 31) {
                    // coarse Y = 0, nametable not switched
                    y = 0;
                }
                else
                {
                    // increment coarse Y
                    y++;
                }
                // put coarse Y back into v
                _v = (ushort) ((_v & 0xFC1F) | (y << 5));

            }
        }

        private void IncrementX()
        {
            // increment hori(v)
            // if coarse X == 31
            if ((_v & 0x001F) == 31)
            {
                // coarse X = 0
                _v &= 0xFFE0;

                // switch horizontal nametable
                _v ^= 0x0400;
            }
            else
            {
                // increment coarse X
                _v++;

            }

        }

        public byte ReadRegister(ushort address)
        {
            switch (address) {
                case 0x2002:
                    return ReadStatus();
                case 0x2004:
                    return ReadOamData();
                case 0x2007:
                    return ReadData();
            }
            return 0;
        }

        private byte ReadData()
        {
        }

        private byte ReadOamData()
        {
            return _oamData[_oamAddress];
        }

        private byte ReadStatus()
        {
            byte result = (byte) (_register & 0x1F);
            result |= (byte) (_spriteOverflow << 5);
            result |= (byte) (_spriteZeroHit << 6);
            if (_nmiOccurred)
            {
                result |= 1 << 7;
            }
            _nmiOccurred = false;
            NmiChange();
            _w = 0;
            return result;
        }
    }
}