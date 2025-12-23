using System;

namespace corenes
{
    class Apu
    {
        private Cpu _cpu;

        // Pulse channels
        private PulseChannel _pulse1;
        private PulseChannel _pulse2;

        // Triangle channel
        private TriangleChannel _triangle;

        // Noise channel
        private NoiseChannel _noise;

        // DMC (simplified/stubbed)
        private bool _dmcEnabled;

        // Frame counter
        private int _framePeriod;
        private int _frameValue;
        private bool _frameIRQ;

        // Cycle counter
        private ulong _cycle;

        // Audio buffer
        private float[] _sampleBuffer = new float[4096];
        private int _sampleIndex;

        // Sample rate
        private const int SAMPLE_RATE = 44100;
        private const double CPU_FREQUENCY = 1789773.0; // NTSC CPU frequency
        private double _sampleTimer;
        private double _samplePeriod;

        public Apu(Cpu cpu)
        {
            _cpu = cpu;
            _pulse1 = new PulseChannel();
            _pulse2 = new PulseChannel();
            _triangle = new TriangleChannel();
            _noise = new NoiseChannel();

            _samplePeriod = CPU_FREQUENCY / SAMPLE_RATE;
            _sampleTimer = 0;
        }

        public void Reset()
        {
            _pulse1.enabled = false;
            _pulse2.enabled = false;
            _triangle.enabled = false;
            _noise.enabled = false;
            _cycle = 0;
            _sampleIndex = 0;
            _sampleTimer = 0;
        }

        public void Step()
        {
            ulong cycle1 = _cycle;
            _cycle++;
            ulong cycle2 = _cycle;

            // Step frame counter every other CPU cycle
            StepFrameCounter();

            // Step timers
            if (cycle2 % 2 == 0)
            {
                _pulse1.StepTimer();
                _pulse2.StepTimer();
                _noise.StepTimer();
            }

            _triangle.StepTimer();

            // Sample output
            _sampleTimer += 1.0;
            if (_sampleTimer >= _samplePeriod)
            {
                _sampleTimer -= _samplePeriod;

                float output = MixOutput();
                if (_sampleIndex < _sampleBuffer.Length)
                {
                    _sampleBuffer[_sampleIndex++] = output;
                }
            }
        }

        private void StepFrameCounter()
        {
            if (_framePeriod == 4)
            {
                _frameValue = (_frameValue + 1) % 14915;

                switch (_frameValue)
                {
                    case 3729:
                        StepEnvelope();
                        break;
                    case 7457:
                        StepEnvelope();
                        StepSweep();
                        StepLength();
                        break;
                    case 11186:
                        StepEnvelope();
                        break;
                    case 14915:
                        if (!_frameIRQ)
                        {
                            _cpu.TriggerIRQ();
                        }
                        StepEnvelope();
                        StepSweep();
                        StepLength();
                        break;
                }
            }
            else
            {
                _frameValue = (_frameValue + 1) % 18641;

                switch (_frameValue)
                {
                    case 3729:
                        StepEnvelope();
                        break;
                    case 7457:
                        StepEnvelope();
                        StepSweep();
                        StepLength();
                        break;
                    case 11186:
                        StepEnvelope();
                        break;
                    case 18641:
                        StepEnvelope();
                        StepSweep();
                        StepLength();
                        break;
                }
            }
        }

        private void StepEnvelope()
        {
            _pulse1.StepEnvelope();
            _pulse2.StepEnvelope();
            _triangle.StepCounter();
            _noise.StepEnvelope();
        }

        private void StepSweep()
        {
            _pulse1.StepSweep();
            _pulse2.StepSweep();
        }

        private void StepLength()
        {
            _pulse1.StepLength();
            _pulse2.StepLength();
            _triangle.StepLength();
            _noise.StepLength();
        }

        private float MixOutput()
        {
            float p1 = _pulse1.Output();
            float p2 = _pulse2.Output();
            float t = _triangle.Output();
            float n = _noise.Output();

            // NES APU mixing formulas
            float pulseOut = 0;
            if (p1 + p2 > 0)
            {
                pulseOut = 95.88f / ((8128.0f / (p1 + p2)) + 100.0f);
            }

            float tndOut = 0;
            if (t + n > 0)
            {
                tndOut = 159.79f / ((1.0f / (t / 8227.0f + n / 12241.0f)) + 100.0f);
            }

            return pulseOut + tndOut;
        }

        public float[] GetSamples()
        {
            float[] samples = new float[_sampleIndex];
            Array.Copy(_sampleBuffer, samples, _sampleIndex);
            _sampleIndex = 0;
            return samples;
        }

        public void WriteRegister(ushort address, byte value)
        {
            switch (address)
            {
                case 0x4000:
                    _pulse1.WriteControl(value);
                    break;
                case 0x4001:
                    _pulse1.WriteSweep(value);
                    break;
                case 0x4002:
                    _pulse1.WriteTimerLow(value);
                    break;
                case 0x4003:
                    _pulse1.WriteTimerHigh(value);
                    break;

                case 0x4004:
                    _pulse2.WriteControl(value);
                    break;
                case 0x4005:
                    _pulse2.WriteSweep(value);
                    break;
                case 0x4006:
                    _pulse2.WriteTimerLow(value);
                    break;
                case 0x4007:
                    _pulse2.WriteTimerHigh(value);
                    break;

                case 0x4008:
                    _triangle.WriteControl(value);
                    break;
                case 0x400A:
                    _triangle.WriteTimerLow(value);
                    break;
                case 0x400B:
                    _triangle.WriteTimerHigh(value);
                    break;

                case 0x400C:
                    _noise.WriteControl(value);
                    break;
                case 0x400E:
                    _noise.WritePeriod(value);
                    break;
                case 0x400F:
                    _noise.WriteLength(value);
                    break;

                case 0x4015:
                    _pulse1.enabled = (value & 0x01) != 0;
                    _pulse2.enabled = (value & 0x02) != 0;
                    _triangle.enabled = (value & 0x04) != 0;
                    _noise.enabled = (value & 0x08) != 0;
                    _dmcEnabled = (value & 0x10) != 0;

                    if (!_pulse1.enabled) _pulse1.lengthValue = 0;
                    if (!_pulse2.enabled) _pulse2.lengthValue = 0;
                    if (!_triangle.enabled) _triangle.lengthValue = 0;
                    if (!_noise.enabled) _noise.lengthValue = 0;
                    break;

                case 0x4017:
                    _framePeriod = ((value >> 7) & 1) == 1 ? 5 : 4;
                    _frameIRQ = ((value >> 6) & 1) == 0;
                    break;
            }
        }

        public byte ReadStatus()
        {
            byte result = 0;
            if (_pulse1.lengthValue > 0) result |= 0x01;
            if (_pulse2.lengthValue > 0) result |= 0x02;
            if (_triangle.lengthValue > 0) result |= 0x04;
            if (_noise.lengthValue > 0) result |= 0x08;
            return result;
        }
    }

    // Pulse channel implementation
    class PulseChannel
    {
        public bool enabled;
        private byte dutyMode;
        private byte duty;
        private bool lengthEnabled;
        private bool envelopeLoop;
        private bool envelopeEnabled;
        private byte envelopePeriod;
        private byte envelopeValue;
        private byte envelopeVolume;
        private bool sweepEnabled;
        private byte sweepPeriod;
        private bool sweepNegate;
        private byte sweepShift;
        private bool sweepReload;
        private byte sweepValue;
        private ushort timerPeriod;
        private ushort timerValue;
        public int lengthValue;

        private static readonly byte[][] dutyTable = new byte[][]
        {
            new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 },
            new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 },
            new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 }
        };

        private static readonly byte[] lengthTable = new byte[]
        {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        public void WriteControl(byte value)
        {
            dutyMode = (byte)((value >> 6) & 3);
            lengthEnabled = ((value >> 5) & 1) == 0;
            envelopeLoop = ((value >> 5) & 1) == 1;
            envelopeEnabled = ((value >> 4) & 1) == 0;
            envelopePeriod = (byte)(value & 15);
            envelopeVolume = (byte)(value & 15);
        }

        public void WriteSweep(byte value)
        {
            sweepEnabled = ((value >> 7) & 1) == 1;
            sweepPeriod = (byte)(((value >> 4) & 7) + 1);
            sweepNegate = ((value >> 3) & 1) == 1;
            sweepShift = (byte)(value & 7);
            sweepReload = true;
        }

        public void WriteTimerLow(byte value)
        {
            timerPeriod = (ushort)((timerPeriod & 0xFF00) | value);
        }

        public void WriteTimerHigh(byte value)
        {
            lengthValue = lengthTable[value >> 3];
            timerPeriod = (ushort)((timerPeriod & 0x00FF) | ((value & 7) << 8));
            envelopeValue = 15;
            duty = 0;
        }

        public void StepTimer()
        {
            if (timerValue == 0)
            {
                timerValue = timerPeriod;
                duty = (byte)((duty + 1) % 8);
            }
            else
            {
                timerValue--;
            }
        }

        public void StepEnvelope()
        {
            if (envelopeValue > 0)
            {
                envelopeValue--;
            }
            else
            {
                if (envelopeLoop)
                {
                    envelopeValue = 15;
                }
            }
        }

        public void StepSweep()
        {
            if (sweepReload)
            {
                if (sweepEnabled && sweepValue == 0)
                {
                    Sweep();
                }
                sweepValue = sweepPeriod;
                sweepReload = false;
            }
            else if (sweepValue > 0)
            {
                sweepValue--;
            }
            else
            {
                if (sweepEnabled)
                {
                    Sweep();
                }
                sweepValue = sweepPeriod;
            }
        }

        private void Sweep()
        {
            int delta = timerPeriod >> sweepShift;
            if (sweepNegate)
            {
                timerPeriod = (ushort)(timerPeriod - delta);
            }
            else
            {
                timerPeriod = (ushort)(timerPeriod + delta);
            }
        }

        public void StepLength()
        {
            if (lengthEnabled && lengthValue > 0)
            {
                lengthValue--;
            }
        }

        public float Output()
        {
            if (!enabled || lengthValue == 0 || dutyTable[dutyMode][duty] == 0)
            {
                return 0;
            }
            if (timerPeriod < 8 || timerPeriod > 0x7FF)
            {
                return 0;
            }
            if (envelopeEnabled)
            {
                return envelopeVolume;
            }
            else
            {
                return envelopeValue;
            }
        }
    }

    // Triangle channel implementation
    class TriangleChannel
    {
        public bool enabled;
        private bool lengthEnabled;
        private byte counterPeriod;
        private byte counterValue;
        private bool counterReload;
        private ushort timerPeriod;
        private ushort timerValue;
        public int lengthValue;
        private byte dutyValue;

        private static readonly byte[] triangleTable = new byte[]
        {
            15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

        private static readonly byte[] lengthTable = new byte[]
        {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        public void WriteControl(byte value)
        {
            lengthEnabled = ((value >> 7) & 1) == 0;
            counterPeriod = (byte)(value & 0x7F);
        }

        public void WriteTimerLow(byte value)
        {
            timerPeriod = (ushort)((timerPeriod & 0xFF00) | value);
        }

        public void WriteTimerHigh(byte value)
        {
            lengthValue = lengthTable[value >> 3];
            timerPeriod = (ushort)((timerPeriod & 0x00FF) | ((value & 7) << 8));
            timerValue = timerPeriod;
            counterReload = true;
        }

        public void StepTimer()
        {
            if (timerValue == 0)
            {
                timerValue = timerPeriod;
                if (lengthValue > 0 && counterValue > 0)
                {
                    dutyValue = (byte)((dutyValue + 1) % 32);
                }
            }
            else
            {
                timerValue--;
            }
        }

        public void StepLength()
        {
            if (lengthEnabled && lengthValue > 0)
            {
                lengthValue--;
            }
        }

        public void StepCounter()
        {
            if (counterReload)
            {
                counterValue = counterPeriod;
            }
            else if (counterValue > 0)
            {
                counterValue--;
            }

            if (lengthEnabled)
            {
                counterReload = false;
            }
        }

        public float Output()
        {
            if (!enabled || lengthValue == 0)
            {
                return 0;
            }
            return triangleTable[dutyValue];
        }
    }

    // Noise channel implementation
    class NoiseChannel
    {
        public bool enabled;
        private bool mode;
        private ushort shiftRegister = 1;
        private bool lengthEnabled;
        private bool envelopeLoop;
        private bool envelopeEnabled;
        private byte envelopePeriod;
        private byte envelopeValue;
        private byte envelopeVolume;
        private ushort timerPeriod;
        private ushort timerValue;
        public int lengthValue;

        private static readonly ushort[] noiseTable = new ushort[]
        {
            4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
        };

        private static readonly byte[] lengthTable = new byte[]
        {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        public void WriteControl(byte value)
        {
            lengthEnabled = ((value >> 5) & 1) == 0;
            envelopeLoop = ((value >> 5) & 1) == 1;
            envelopeEnabled = ((value >> 4) & 1) == 0;
            envelopePeriod = (byte)(value & 15);
            envelopeVolume = (byte)(value & 15);
        }

        public void WritePeriod(byte value)
        {
            mode = ((value >> 7) & 1) == 1;
            timerPeriod = noiseTable[value & 0x0F];
        }

        public void WriteLength(byte value)
        {
            lengthValue = lengthTable[value >> 3];
            envelopeValue = 15;
        }

        public void StepTimer()
        {
            if (timerValue == 0)
            {
                timerValue = timerPeriod;

                byte shift = mode ? (byte)6 : (byte)1;
                byte b1 = (byte)(shiftRegister & 1);
                byte b2 = (byte)((shiftRegister >> shift) & 1);
                shiftRegister >>= 1;
                shiftRegister |= (ushort)((b1 ^ b2) << 14);
            }
            else
            {
                timerValue--;
            }
        }

        public void StepEnvelope()
        {
            if (envelopeValue > 0)
            {
                envelopeValue--;
            }
            else
            {
                if (envelopeLoop)
                {
                    envelopeValue = 15;
                }
            }
        }

        public void StepLength()
        {
            if (lengthEnabled && lengthValue > 0)
            {
                lengthValue--;
            }
        }

        public float Output()
        {
            if (!enabled || lengthValue == 0 || (shiftRegister & 1) == 1)
            {
                return 0;
            }
            if (envelopeEnabled)
            {
                return envelopeVolume;
            }
            else
            {
                return envelopeValue;
            }
        }
    }
}
