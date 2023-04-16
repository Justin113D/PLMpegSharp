using PLMpegSharp.Container;
using PLMpegSharp.LUT;

namespace PLMpegSharp
{
    /// <summary>
    /// MP2 Audio decoder. <br/>
    /// Based on kjmp2 by Martin J. Fiedler <br/>
    /// http://keyj.emphy.de/kjmp2/
    /// </summary>
    public class AudioDecoder
    {
        #region Private Fields

        private class SampleBlock
        {
            public QuantizerSpec? Allocation { get; set; }
            public byte ScaleFactorInfo { get; set; }
            public int[] ScaleFactor { get; }
            public int[] Sample { get; }

            public SampleBlock()
            {
                ScaleFactor = new int[3];
                Sample = new int[3];
            }
        }

        public const int FrameSync = 0x7ff;

        public const int MPEG_2_5 = 0x0;
        public const int MPEG_2 = 0x2;
        public const int MPEG_1 = 0x3;

        public const int Layer3 = 0x1;
        public const int Layer2 = 0x2;
        public const int Layer1 = 0x3;

        private readonly DataBuffer _buffer;
        private readonly SampleBlock[][] _sampleBlocks;
        private readonly Samples _samples;
        private readonly float[] _d;
        private readonly float[][] _v;
        private readonly float[] _u;

        private double _time;
        private int _samplesDecoded;
        private int _samplerateIndex;
        private int _bitrateIndex;
        private int _version;
        private int _layer;
        private AudioMode _mode;
        private int _bound;
        private int _vPos;
        private int _nextFrameDataSize;
        private bool _hasHeader;

        #endregion

        #region Public Properties

        /// <summary>
        /// whether a frame header was found and we can accurately report on samplerate.
        /// </summary>
        public bool HasHeader
        {
            get
            {
                if (!_hasHeader)
                {
                    DecodeHeader();
                }
                return _hasHeader;
            }
        }

        /// <summary>
        /// The samplerate in samples per second.
        /// </summary>
        public int Samplerate
            => HasHeader ? AudioLUTs.Samplerate[_samplerateIndex] : 0;

        /// <summary>
        /// The current internal time in seconds. <br/>
        /// Setting this is only useful when you manipulate the underlying 
        /// video buffer and want to enforce a correct timestamps.
        /// </summary>
        public double Time
        {
            get => _time;
            set
            {
                _samplesDecoded = (int)(value * Samplerate);
                _time = value;
            }
        }

        /// <summary>
        /// Whether the file has ended. This will be cleared on <see cref="Rewind"/>.
        /// </summary>
        public bool HasEnded
            => _buffer.HasEnded;

        #endregion

        #region Constructor

        /// <summary>
        /// Create an audio decoder with a <see cref="DataBuffer"/> as source.
        /// </summary>
        /// <param name="buffer"></param>
        public AudioDecoder(DataBuffer buffer)
        {
            _buffer = buffer;

            _sampleBlocks = new SampleBlock[][] { new SampleBlock[32], new SampleBlock[32] };
            for (int i = 0; i < 32; i++)
            {
                _sampleBlocks[0][i] = new();
                _sampleBlocks[1][i] = new();
            }

            _samples = new();
            _d = new float[1024];
            _v = new float[][] { new float[1024], new float[1024] };
            _u = new float[32];

            _samplerateIndex = 3; // indicates 0
            Array.Copy(AudioLUTs.SynthesisWindow, _d, AudioLUTs.SynthesisWindow.Length);
            Array.Copy(AudioLUTs.SynthesisWindow, 0, _d, AudioLUTs.SynthesisWindow.Length, AudioLUTs.SynthesisWindow.Length);

            // attempt to decode the first header
            _nextFrameDataSize = DecodeHeader();
        }

        #endregion

        #region Private Methods

        private bool FindFrameSync()
        {
            nuint i;
            for (i = _buffer.BitIndex >> 3; i < _buffer.Length - 1; i++)
            {
                if (
                    _buffer.Bytes[(int)i] == 0xFF &&
                    (_buffer.Bytes[(int)i + 1] & 0xFE) == 0xFC
                )
                {
                    _buffer.BitIndex = ((i + 1) << 3) + 3;
                    return true;
                }
            }
            _buffer.BitIndex = (i + 1) << 3;
            return false;
        }

        private int DecodeHeader()
        {
            if (!_buffer.Has(48))
            {
                return 0;
            }

            _buffer.SkipBytes(0x00);
            int sync = (int)_buffer.Read(11);


            // Attempt to resync if no syncword was found. This sucks balls. The MP2
            // stream contains a syncword just before every frame (11 bits set to 1).
            // However, this syncword is not guaranteed to not occur elsewhere in the
            // stream. So, if we have to resync, we also have to check if the header
            // (samplerate, bitrate) differs from the one we had before. This all
            // may still lead to garbage data being decoded :/

            if (sync != FrameSync && !FindFrameSync())
            {
                return 0;
            }

            _version = (int)_buffer.Read(2);
            _layer = (int)_buffer.Read(2);
            bool hasCRC = _buffer.Read(1) == 0;

            if (
                _version != MPEG_1 ||
                _layer != Layer2
            )
            {
                return 0;
            }

            int bitrateIndex = (int)_buffer.Read(4) - 1;
            if (bitrateIndex > 13)
            {
                return 0;
            }

            int samplerateIndex = (int)_buffer.Read(2);
            if (samplerateIndex == 3)
            {
                return 0;
            }

            int padding = (int)_buffer.Read(1);
            _buffer.Skip(1); // f_private
            AudioMode mode = (AudioMode)_buffer.Read(2);

            // If we already have a header, make sure the samplerate, bitrate and mode
            // are still the same, otherwise we might have missed sync.
            if (
                _hasHeader && (
                    _bitrateIndex != bitrateIndex ||
                    _samplerateIndex != samplerateIndex ||
                    _mode != mode
                )
            )
            {
                return 0;
            }

            _bitrateIndex = bitrateIndex;
            _samplerateIndex = samplerateIndex;
            _mode = mode;
            _hasHeader = true;

            // Parse the mode_extension, set up the stereo bound
            if (mode == AudioMode.JointStereo)
            {
                _bound = ((int)_buffer.Read(2) + 1) << 2;
            }
            else
            {
                _buffer.Skip(2);
                _bound = (mode == AudioMode.Mono) ? 0 : 32;
            }

            // Discard the last 4 bits of the header and the CRC value, if present
            _buffer.Skip(4); // copyright(1), original(1), emphasis(2)
            if (hasCRC)
            {
                _buffer.Skip(16);
            }

            // Compute frame size, check if we have enough data to decode the whole
            // frame.
            int bitrate = AudioLUTs.BitRate[_bitrateIndex];
            int samplerate = AudioLUTs.Samplerate[_samplerateIndex];
            int frameSize = (144000 * bitrate / samplerate) + padding;
            return frameSize - (hasCRC ? 6 : 4);
        }

        private void DecodeFrame()
        {
            // Prepare the quantizer table lookups
            int tab1 = (_mode == AudioMode.Mono) ? 0 : 1;
            int tab2 = AudioLUTs.QuantLutStep1[tab1][_bitrateIndex];
            int tab3 = AudioLUTs.QuantLutStep2[tab2][_samplerateIndex];
            int sblimit = tab3 & 63;
            tab3 >>= 6;

            if (_bound > sblimit)
            {
                _bound = sblimit;
            }

            // Read the allocation information
            for (int sb = 0; sb < _bound; sb++)
            {
                _sampleBlocks[0][sb].Allocation = ReadAllocation(sb, tab3);
                _sampleBlocks[1][sb].Allocation = ReadAllocation(sb, tab3);
            }

            for (int sb = _bound; sb < sblimit; sb++)
            {
                _sampleBlocks[0][sb].Allocation =
                    _sampleBlocks[1][sb].Allocation =
                    ReadAllocation(sb, tab3);
            }

            // Read scale factor selector information
            int channels = (_mode == AudioMode.Mono) ? 1 : 2;
            for (int sb = 0; sb < sblimit; sb++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    SampleBlock sampleBlock = _sampleBlocks[ch][sb];
                    if (sampleBlock.Allocation != null)
                    {
                        sampleBlock.ScaleFactorInfo = (byte)_buffer.Read(2);
                    }
                }
                if (_mode == AudioMode.Mono)
                {
                    _sampleBlocks[1][sb].ScaleFactorInfo = _sampleBlocks[0][sb].ScaleFactorInfo;
                }
            }

            // Read scale factors
            for (int sb = 0; sb < sblimit; sb++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    SampleBlock sample = _sampleBlocks[ch][sb];

                    if (_sampleBlocks[ch][sb].Allocation != null)
                    {
                        int[] sf = sample.ScaleFactor;
                        switch (sample.ScaleFactorInfo)
                        {
                            case 0:
                                sf[0] = (int)_buffer.Read(6);
                                sf[1] = (int)_buffer.Read(6);
                                sf[2] = (int)_buffer.Read(6);
                                break;
                            case 1:
                                sf[0] =
                                sf[1] = (int)_buffer.Read(6);
                                sf[2] = (int)_buffer.Read(6);
                                break;
                            case 2:
                                sf[0] =
                                sf[1] =
                                sf[2] = (int)_buffer.Read(6);
                                break;
                            case 3:
                                sf[0] = (int)_buffer.Read(6);
                                sf[1] =
                                sf[2] = (int)_buffer.Read(6);
                                break;
                        }
                    }
                }
                if (_mode == AudioMode.Mono)
                {
                    SampleBlock left = _sampleBlocks[0][sb];
                    SampleBlock right = _sampleBlocks[1][sb];

                    right.ScaleFactor[0] = left.ScaleFactor[0];
                    right.ScaleFactor[1] = left.ScaleFactor[1];
                    right.ScaleFactor[2] = left.ScaleFactor[2];
                }
            }

            // Coefficient input and reconstruction
            int out_pos = 0;
            for (int part = 0; part < 3; part++)
            {
                for (int granule = 0; granule < 4; granule++)
                {

                    // Read the samples
                    for (int sb = 0; sb < _bound; sb++)
                    {
                        ReadSamples(0, sb, part);
                        ReadSamples(1, sb, part);
                    }

                    for (int sb = _bound; sb < sblimit; sb++)
                    {
                        ReadSamples(0, sb, part);
                        SampleBlock left = _sampleBlocks[0][sb];
                        SampleBlock right = _sampleBlocks[1][sb];

                        right.Sample[0] = left.Sample[0];
                        right.Sample[1] = left.Sample[1];
                        right.Sample[2] = left.Sample[2];
                    }

                    for (int sb = sblimit; sb < 32; sb++)
                    {
                        Array.Clear(_sampleBlocks[0][sb].Sample);
                        Array.Clear(_sampleBlocks[1][sb].Sample);
                    }

                    // Synthesis loop
                    for (int p = 0; p < 3; p++)
                    {
                        // Shifting step
                        _vPos = (_vPos - 64) & 1023;

                        for (int ch = 0; ch < 2; ch++)
                        {
                            IDCT36(_sampleBlocks[ch], p, _v[ch], _vPos);

                            // Build U, windowing, calculate output
                            Array.Clear(_u);

                            int dIndex = 512 - (_vPos >> 1);
                            int vIndex = (_vPos % 128) >> 1;
                            while (vIndex < 1024)
                            {
                                for (int i = 0; i < 32; ++i)
                                {
                                    _u[i] += _d[dIndex++] * _v[ch][vIndex++];
                                }

                                vIndex += 128 - 32;
                                dIndex += 64 - 32;
                            }

                            dIndex -= (512 - 32);
                            vIndex = (128 - 32 + 1024) - vIndex;
                            while (vIndex < 1024)
                            {
                                for (int i = 0; i < 32; ++i)
                                {
                                    _u[i] += _d[dIndex++] * _v[ch][vIndex++];
                                }

                                vIndex += 128 - 32;
                                dIndex += 64 - 32;
                            }

                            // Output samples
                            float[] outChannel = ch == 0
                                ? _samples.Left
                                : _samples.Right;

                            for (int j = 0; j < 32; j++)
                            {
                                outChannel[out_pos + j] = _u[j] / 2147418112.0f;
                            }

                        } // End of synthesis channel loop

                        out_pos += 32;
                    } // End of synthesis sub-block loop

                } // Decoding of the granule finished
            }

            _buffer.Align();
        }

        private QuantizerSpec? ReadAllocation(int sb, int tab3)
        {
            nuint tab4 = AudioLUTs.QuantLutStep3[tab3][sb];
            int qtab = AudioLUTs.QuantLutStep4[tab4 & 15][_buffer.Read(tab4 >> 4)];
            return qtab != 0 ? (AudioLUTs.QuantTab[qtab - 1]) : null;
        }

        private void ReadSamples(int ch, int sb, int part)
        {
            SampleBlock sampleStruct = _sampleBlocks[ch][sb];

            QuantizerSpec? q = sampleStruct.Allocation;
            int sf = sampleStruct.ScaleFactor[part];
            int[] sample = sampleStruct.Sample;
            int val;

            if (q == null)
            {
                // No bits allocated for this subband
                sample[0] = sample[1] = sample[2] = 0;
                return;
            }

            // Resolve scalefactor
            if (sf == 63)
            {
                sf = 0;
            }
            else
            {
                int shift = (sf / 3) | 0;
                sf = (AudioLUTs.ScalefactorBase[sf % 3] + ((1 << shift) >> 1)) >> shift;
            }

            // Decode samples
            int adj = q.Value.levels;
            if (q.Value.group)
            {
                // Decode grouped samples
                val = (int)_buffer.Read(q.Value.bits);
                sample[0] = val % adj;
                val /= adj;
                sample[1] = val % adj;
                sample[2] = val / adj;
            }
            else
            {
                // Decode direct samples
                sample[0] = (int)_buffer.Read(q.Value.bits);
                sample[1] = (int)_buffer.Read(q.Value.bits);
                sample[2] = (int)_buffer.Read(q.Value.bits);
            }

            // Postmultiply samples
            int scale = 65536 / (adj + 1);
            adj = ((adj + 1) >> 1) - 1;

            val = (adj - sample[0]) * scale;
            sample[0] = (val * (sf >> 12) + ((val * (sf & 4095) + 2048) >> 12)) >> 12;

            val = (adj - sample[1]) * scale;
            sample[1] = (val * (sf >> 12) + ((val * (sf & 4095) + 2048) >> 12)) >> 12;

            val = (adj - sample[2]) * scale;
            sample[2] = (val * (sf >> 12) + ((val * (sf & 4095) + 2048) >> 12)) >> 12;
        }

        private static void IDCT36(SampleBlock[] s, int ss, float[] d, int dp)
        {
            float t01, t02, t03, t04, t05, t06, t07, t08, t09, t10, t11, t12,
                  t13, t14, t15, t16, t17, t18, t19, t20, t21, t22, t23, t24,
                  t25, t26, t27, t28, t29, t30, t31, t32, t33;

            t01 = (s[0].Sample[ss] + s[31].Sample[ss]); t02 = (s[0].Sample[ss] - s[31].Sample[ss]) * 0.500602998235f;
            t03 = (s[1].Sample[ss] + s[30].Sample[ss]); t04 = (s[1].Sample[ss] - s[30].Sample[ss]) * 0.505470959898f;
            t05 = (s[2].Sample[ss] + s[29].Sample[ss]); t06 = (s[2].Sample[ss] - s[29].Sample[ss]) * 0.515447309923f;
            t07 = (s[3].Sample[ss] + s[28].Sample[ss]); t08 = (s[3].Sample[ss] - s[28].Sample[ss]) * 0.53104259109f;
            t09 = (s[4].Sample[ss] + s[27].Sample[ss]); t10 = (s[4].Sample[ss] - s[27].Sample[ss]) * 0.553103896034f;
            t11 = (s[5].Sample[ss] + s[26].Sample[ss]); t12 = (s[5].Sample[ss] - s[26].Sample[ss]) * 0.582934968206f;
            t13 = (s[6].Sample[ss] + s[25].Sample[ss]); t14 = (s[6].Sample[ss] - s[25].Sample[ss]) * 0.622504123036f;
            t15 = (s[7].Sample[ss] + s[24].Sample[ss]); t16 = (s[7].Sample[ss] - s[24].Sample[ss]) * 0.674808341455f;
            t17 = (s[8].Sample[ss] + s[23].Sample[ss]); t18 = (s[8].Sample[ss] - s[23].Sample[ss]) * 0.744536271002f;
            t19 = (s[9].Sample[ss] + s[22].Sample[ss]); t20 = (s[9].Sample[ss] - s[22].Sample[ss]) * 0.839349645416f;
            t21 = (s[10].Sample[ss] + s[21].Sample[ss]); t22 = (s[10].Sample[ss] - s[21].Sample[ss]) * 0.972568237862f;
            t23 = (s[11].Sample[ss] + s[20].Sample[ss]); t24 = (s[11].Sample[ss] - s[20].Sample[ss]) * 1.16943993343f;
            t25 = (s[12].Sample[ss] + s[19].Sample[ss]); t26 = (s[12].Sample[ss] - s[19].Sample[ss]) * 1.48416461631f;
            t27 = (s[13].Sample[ss] + s[18].Sample[ss]); t28 = (s[13].Sample[ss] - s[18].Sample[ss]) * 2.05778100995f;
            t29 = (s[14].Sample[ss] + s[17].Sample[ss]); t30 = (s[14].Sample[ss] - s[17].Sample[ss]) * 3.40760841847f;
            t31 = (s[15].Sample[ss] + s[16].Sample[ss]); t32 = (s[15].Sample[ss] - s[16].Sample[ss]) * 10.1900081235f;

            t33 = t01 + t31; t31 = (t01 - t31) * 0.502419286188f;
            t01 = t03 + t29; t29 = (t03 - t29) * 0.52249861494f;
            t03 = t05 + t27; t27 = (t05 - t27) * 0.566944034816f;
            t05 = t07 + t25; t25 = (t07 - t25) * 0.64682178336f;
            t07 = t09 + t23; t23 = (t09 - t23) * 0.788154623451f;
            t09 = t11 + t21; t21 = (t11 - t21) * 1.06067768599f;
            t11 = t13 + t19; t19 = (t13 - t19) * 1.72244709824f;
            t13 = t15 + t17; t17 = (t15 - t17) * 5.10114861869f;
            t15 = t33 + t13; t13 = (t33 - t13) * 0.509795579104f;
            t33 = t01 + t11; t01 = (t01 - t11) * 0.601344886935f;
            t11 = t03 + t09; t09 = (t03 - t09) * 0.899976223136f;
            t03 = t05 + t07; t07 = (t05 - t07) * 2.56291544774f;
            t05 = t15 + t03; t15 = (t15 - t03) * 0.541196100146f;
            t03 = t33 + t11; t11 = (t33 - t11) * 1.30656296488f;
            t33 = t05 + t03; t05 = (t05 - t03) * 0.707106781187f;
            t03 = t15 + t11; t15 = (t15 - t11) * 0.707106781187f;
            t03 += t15;
            t11 = t13 + t07; t13 = (t13 - t07) * 0.541196100146f;
            t07 = t01 + t09; t09 = (t01 - t09) * 1.30656296488f;
            t01 = t11 + t07; t07 = (t11 - t07) * 0.707106781187f;
            t11 = t13 + t09; t13 = (t13 - t09) * 0.707106781187f;
            t11 += t13; t01 += t11;
            t11 += t07; t07 += t13;
            t09 = t31 + t17; t31 = (t31 - t17) * 0.509795579104f;
            t17 = t29 + t19; t29 = (t29 - t19) * 0.601344886935f;
            t19 = t27 + t21; t21 = (t27 - t21) * 0.899976223136f;
            t27 = t25 + t23; t23 = (t25 - t23) * 2.56291544774f;
            t25 = t09 + t27; t09 = (t09 - t27) * 0.541196100146f;
            t27 = t17 + t19; t19 = (t17 - t19) * 1.30656296488f;
            t17 = t25 + t27; t27 = (t25 - t27) * 0.707106781187f;
            t25 = t09 + t19; t19 = (t09 - t19) * 0.707106781187f;
            t25 += t19;
            t09 = t31 + t23; t31 = (t31 - t23) * 0.541196100146f;
            t23 = t29 + t21; t21 = (t29 - t21) * 1.30656296488f;
            t29 = t09 + t23; t23 = (t09 - t23) * 0.707106781187f;
            t09 = t31 + t21; t31 = (t31 - t21) * 0.707106781187f;
            t09 += t31; t29 += t09; t09 += t23; t23 += t31;
            t17 += t29; t29 += t25; t25 += t09; t09 += t27;
            t27 += t23; t23 += t19; t19 += t31;
            t21 = t02 + t32; t02 = (t02 - t32) * 0.502419286188f;
            t32 = t04 + t30; t04 = (t04 - t30) * 0.52249861494f;
            t30 = t06 + t28; t28 = (t06 - t28) * 0.566944034816f;
            t06 = t08 + t26; t08 = (t08 - t26) * 0.64682178336f;
            t26 = t10 + t24; t10 = (t10 - t24) * 0.788154623451f;
            t24 = t12 + t22; t22 = (t12 - t22) * 1.06067768599f;
            t12 = t14 + t20; t20 = (t14 - t20) * 1.72244709824f;
            t14 = t16 + t18; t16 = (t16 - t18) * 5.10114861869f;
            t18 = t21 + t14; t14 = (t21 - t14) * 0.509795579104f;
            t21 = t32 + t12; t32 = (t32 - t12) * 0.601344886935f;
            t12 = t30 + t24; t24 = (t30 - t24) * 0.899976223136f;
            t30 = t06 + t26; t26 = (t06 - t26) * 2.56291544774f;
            t06 = t18 + t30; t18 = (t18 - t30) * 0.541196100146f;
            t30 = t21 + t12; t12 = (t21 - t12) * 1.30656296488f;
            t21 = t06 + t30; t30 = (t06 - t30) * 0.707106781187f;
            t06 = t18 + t12; t12 = (t18 - t12) * 0.707106781187f;
            t06 += t12;
            t18 = t14 + t26; t26 = (t14 - t26) * 0.541196100146f;
            t14 = t32 + t24; t24 = (t32 - t24) * 1.30656296488f;
            t32 = t18 + t14; t14 = (t18 - t14) * 0.707106781187f;
            t18 = t26 + t24; t24 = (t26 - t24) * 0.707106781187f;
            t18 += t24; t32 += t18;
            t18 += t14; t26 = t14 + t24;
            t14 = t02 + t16; t02 = (t02 - t16) * 0.509795579104f;
            t16 = t04 + t20; t04 = (t04 - t20) * 0.601344886935f;
            t20 = t28 + t22; t22 = (t28 - t22) * 0.899976223136f;
            t28 = t08 + t10; t10 = (t08 - t10) * 2.56291544774f;
            t08 = t14 + t28; t14 = (t14 - t28) * 0.541196100146f;
            t28 = t16 + t20; t20 = (t16 - t20) * 1.30656296488f;
            t16 = t08 + t28; t28 = (t08 - t28) * 0.707106781187f;
            t08 = t14 + t20; t20 = (t14 - t20) * 0.707106781187f;
            t08 += t20;
            t14 = t02 + t10; t02 = (t02 - t10) * 0.541196100146f;
            t10 = t04 + t22; t22 = (t04 - t22) * 1.30656296488f;
            t04 = t14 + t10; t10 = (t14 - t10) * 0.707106781187f;
            t14 = t02 + t22; t02 = (t02 - t22) * 0.707106781187f;
            t14 += t02; t04 += t14; t14 += t10; t10 += t02;
            t16 += t04; t04 += t08; t08 += t14; t14 += t28;
            t28 += t10; t10 += t20; t20 += t02; t21 += t16;
            t16 += t32; t32 += t04; t04 += t06; t06 += t08;
            t08 += t18; t18 += t14; t14 += t30; t30 += t28;
            t28 += t26; t26 += t10; t10 += t12; t12 += t20;
            t20 += t24; t24 += t02;

            d[dp + 48] = -t33;
            d[dp + 49] = d[dp + 47] = -t21;
            d[dp + 50] = d[dp + 46] = -t17;
            d[dp + 51] = d[dp + 45] = -t16;
            d[dp + 52] = d[dp + 44] = -t01;
            d[dp + 53] = d[dp + 43] = -t32;
            d[dp + 54] = d[dp + 42] = -t29;
            d[dp + 55] = d[dp + 41] = -t04;
            d[dp + 56] = d[dp + 40] = -t03;
            d[dp + 57] = d[dp + 39] = -t06;
            d[dp + 58] = d[dp + 38] = -t25;
            d[dp + 59] = d[dp + 37] = -t08;
            d[dp + 60] = d[dp + 36] = -t11;
            d[dp + 61] = d[dp + 35] = -t18;
            d[dp + 62] = d[dp + 34] = -t09;
            d[dp + 63] = d[dp + 33] = -t14;
            d[dp + 32] = -t05;
            d[dp + 0] = t05; d[dp + 31] = -t30;
            d[dp + 1] = t30; d[dp + 30] = -t27;
            d[dp + 2] = t27; d[dp + 29] = -t28;
            d[dp + 3] = t28; d[dp + 28] = -t07;
            d[dp + 4] = t07; d[dp + 27] = -t26;
            d[dp + 5] = t26; d[dp + 26] = -t23;
            d[dp + 6] = t23; d[dp + 25] = -t10;
            d[dp + 7] = t10; d[dp + 24] = -t15;
            d[dp + 8] = t15; d[dp + 23] = -t12;
            d[dp + 9] = t12; d[dp + 22] = -t19;
            d[dp + 10] = t19; d[dp + 21] = -t20;
            d[dp + 11] = t20; d[dp + 20] = -t13;
            d[dp + 12] = t13; d[dp + 19] = -t24;
            d[dp + 13] = t24; d[dp + 18] = -t31;
            d[dp + 14] = t31; d[dp + 17] = -t02;
            d[dp + 15] = t02; d[dp + 16] = 0.0f;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Rewind the internal buffer. See <see cref="DataBuffer.Rewind"/>.
        /// </summary>
        public void Rewind()
        {
            _buffer.Rewind();
            _time = 0;
            _samplesDecoded = 0;
            _nextFrameDataSize = 0;
        }

        /// <summary>
        /// Decode and return one "frame" of audio and advance the internal time by
        /// (<see cref="Samples.AudioSamplesPerFrame"/>/<see cref="Samplerate"/>) seconds. <br/>
        /// The returned <see cref="Samples"/> is valid until the next call of <see cref="Decode"/>.
        /// </summary>
        public Samples? Decode()
        {
            // Do we have at least enough information to decode the frame header?
            if (_nextFrameDataSize == 0)
            {
                if (!_buffer.Has(48))
                {
                    return null;
                }
                _nextFrameDataSize = DecodeHeader();
            }

            if (
                _nextFrameDataSize == 0 ||
                !_buffer.Has((nuint)_nextFrameDataSize << 3)
            )
            {
                return null;
            }

            DecodeFrame();
            _nextFrameDataSize = 0;

            _samples.Time = _time;

            _samplesDecoded += Samples.AudioSamplesPerFrame;
            _time = _samplesDecoded / (double)AudioLUTs.Samplerate[_samplerateIndex];

            return _samples;
        }

        #endregion
    }
}
