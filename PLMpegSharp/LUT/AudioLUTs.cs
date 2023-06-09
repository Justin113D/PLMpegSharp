﻿namespace PLMpegSharp.LUT
{
    internal static class AudioLUTs
    {
        public static readonly ushort[] Samplerate = {
            44100, 48000, 32000, 0, // MPEG-1
	        22050, 24000, 16000, 0  // MPEG-2
        };

        public static readonly short[] BitRate = {
            32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, // MPEG-1
	         8, 16, 24, 32, 40, 48,  56,  64,  80,  96, 112, 128, 144, 160  // MPEG-2
        };

        public static readonly int[] ScalefactorBase = {
            0x02000000, 0x01965FEA, 0x01428A30
        };

        public static readonly float[] SynthesisWindow = {
                 0.0f,     -0.5f,     -0.5f,     -0.5f,     -0.5f,     -0.5f,
                -0.5f,     -1.0f,     -1.0f,     -1.0f,     -1.0f,     -1.5f,
                -1.5f,     -2.0f,     -2.0f,     -2.5f,     -2.5f,     -3.0f,
                -3.5f,     -3.5f,     -4.0f,     -4.5f,     -5.0f,     -5.5f,
                -6.5f,     -7.0f,     -8.0f,     -8.5f,     -9.5f,    -10.5f,
               -12.0f,    -13.0f,    -14.5f,    -15.5f,    -17.5f,    -19.0f,
               -20.5f,    -22.5f,    -24.5f,    -26.5f,    -29.0f,    -31.5f,
               -34.0f,    -36.5f,    -39.5f,    -42.5f,    -45.5f,    -48.5f,
               -52.0f,    -55.5f,    -58.5f,    -62.5f,    -66.0f,    -69.5f,
               -73.5f,    -77.0f,    -80.5f,    -84.5f,    -88.0f,    -91.5f,
               -95.0f,    -98.0f,   -101.0f,   -104.0f,    106.5f,    109.0f,
               111.0f,    112.5f,    113.5f,    114.0f,    114.0f,    113.5f,
               112.0f,    110.5f,    107.5f,    104.0f,    100.0f,     94.5f,
                88.5f,     81.5f,     73.0f,     63.5f,     53.0f,     41.5f,
                28.5f,     14.5f,     -1.0f,    -18.0f,    -36.0f,    -55.5f,
               -76.5f,    -98.5f,   -122.0f,   -147.0f,   -173.5f,   -200.5f,
              -229.5f,   -259.5f,   -290.5f,   -322.5f,   -355.5f,   -389.5f,
              -424.0f,   -459.5f,   -495.5f,   -532.0f,   -568.5f,   -605.0f,
              -641.5f,   -678.0f,   -714.0f,   -749.0f,   -783.5f,   -817.0f,
              -849.0f,   -879.5f,   -908.5f,   -935.0f,   -959.5f,   -981.0f,
             -1000.5f,  -1016.0f,  -1028.5f,  -1037.5f,  -1042.5f,  -1043.5f,
             -1040.0f,  -1031.5f,   1018.5f,   1000.0f,    976.0f,    946.5f,
               911.0f,    869.5f,    822.0f,    767.5f,    707.0f,    640.0f,
               565.5f,    485.0f,    397.0f,    302.5f,    201.0f,     92.5f,
               -22.5f,   -144.0f,   -272.5f,   -407.0f,   -547.5f,   -694.0f,
              -846.0f,  -1003.0f,  -1165.0f,  -1331.5f,  -1502.0f,  -1675.5f,
             -1852.5f,  -2031.5f,  -2212.5f,  -2394.0f,  -2576.5f,  -2758.5f,
             -2939.5f,  -3118.5f,  -3294.5f,  -3467.5f,  -3635.5f,  -3798.5f,
             -3955.0f,  -4104.5f,  -4245.5f,  -4377.5f,  -4499.0f,  -4609.5f,
             -4708.0f,  -4792.5f,  -4863.5f,  -4919.0f,  -4958.0f,  -4979.5f,
             -4983.0f,  -4967.5f,  -4931.5f,  -4875.0f,  -4796.0f,  -4694.5f,
             -4569.5f,  -4420.0f,  -4246.0f,  -4046.0f,  -3820.0f,  -3567.0f,
              3287.0f,   2979.5f,   2644.0f,   2280.5f,   1888.0f,   1467.5f,
              1018.5f,    541.0f,     35.0f,   -499.0f,  -1061.0f,  -1650.0f,
             -2266.5f,  -2909.0f,  -3577.0f,  -4270.0f,  -4987.5f,  -5727.5f,
             -6490.0f,  -7274.0f,  -8077.5f,  -8899.5f,  -9739.0f, -10594.5f,
            -11464.5f, -12347.0f, -13241.0f, -14144.5f, -15056.0f, -15973.5f,
            -16895.5f, -17820.0f, -18744.5f, -19668.0f, -20588.0f, -21503.0f,
            -22410.5f, -23308.5f, -24195.0f, -25068.5f, -25926.5f, -26767.0f,
            -27589.0f, -28389.0f, -29166.5f, -29919.0f, -30644.5f, -31342.0f,
            -32009.5f, -32645.0f, -33247.0f, -33814.5f, -34346.0f, -34839.5f,
            -35295.0f, -35710.0f, -36084.5f, -36417.5f, -36707.5f, -36954.0f,
            -37156.5f, -37315.0f, -37428.0f, -37496.0f,  37519.0f,  37496.0f,
             37428.0f,  37315.0f,  37156.5f,  36954.0f,  36707.5f,  36417.5f,
             36084.5f,  35710.0f,  35295.0f,  34839.5f,  34346.0f,  33814.5f,
             33247.0f,  32645.0f,  32009.5f,  31342.0f,  30644.5f,  29919.0f,
             29166.5f,  28389.0f,  27589.0f,  26767.0f,  25926.5f,  25068.5f,
             24195.0f,  23308.5f,  22410.5f,  21503.0f,  20588.0f,  19668.0f,
             18744.5f,  17820.0f,  16895.5f,  15973.5f,  15056.0f,  14144.5f,
             13241.0f,  12347.0f,  11464.5f,  10594.5f,   9739.0f,   8899.5f,
              8077.5f,   7274.0f,   6490.0f,   5727.5f,   4987.5f,   4270.0f,
              3577.0f,   2909.0f,   2266.5f,   1650.0f,   1061.0f,    499.0f,
               -35.0f,   -541.0f,  -1018.5f,  -1467.5f,  -1888.0f,  -2280.5f,
             -2644.0f,  -2979.5f,   3287.0f,   3567.0f,   3820.0f,   4046.0f,
              4246.0f,   4420.0f,   4569.5f,   4694.5f,   4796.0f,   4875.0f,
              4931.5f,   4967.5f,   4983.0f,   4979.5f,   4958.0f,   4919.0f,
              4863.5f,   4792.5f,   4708.0f,   4609.5f,   4499.0f,   4377.5f,
              4245.5f,   4104.5f,   3955.0f,   3798.5f,   3635.5f,   3467.5f,
              3294.5f,   3118.5f,   2939.5f,   2758.5f,   2576.5f,   2394.0f,
              2212.5f,   2031.5f,   1852.5f,   1675.5f,   1502.0f,   1331.5f,
              1165.0f,   1003.0f,    846.0f,    694.0f,    547.5f,    407.0f,
               272.5f,    144.0f,     22.5f,    -92.5f,   -201.0f,   -302.5f,
              -397.0f,   -485.0f,   -565.5f,   -640.0f,   -707.0f,   -767.5f,
              -822.0f,   -869.5f,   -911.0f,   -946.5f,   -976.0f,  -1000.0f,
              1018.5f,   1031.5f,   1040.0f,   1043.5f,   1042.5f,   1037.5f,
              1028.5f,   1016.0f,   1000.5f,    981.0f,    959.5f,    935.0f,
               908.5f,    879.5f,    849.0f,    817.0f,    783.5f,    749.0f,
               714.0f,    678.0f,    641.5f,    605.0f,    568.5f,    532.0f,
               495.5f,    459.5f,    424.0f,    389.5f,    355.5f,    322.5f,
               290.5f,    259.5f,    229.5f,    200.5f,    173.5f,    147.0f,
               122.0f,     98.5f,     76.5f,     55.5f,     36.0f,     18.0f,
                 1.0f,    -14.5f,    -28.5f,    -41.5f,    -53.0f,    -63.5f,
               -73.0f,    -81.5f,    -88.5f,    -94.5f,   -100.0f,   -104.0f,
              -107.5f,   -110.5f,   -112.0f,   -113.5f,   -114.0f,   -114.0f,
              -113.5f,   -112.5f,   -111.0f,   -109.0f,    106.5f,    104.0f,
               101.0f,     98.0f,     95.0f,     91.5f,     88.0f,     84.5f,
                80.5f,     77.0f,     73.5f,     69.5f,     66.0f,     62.5f,
                58.5f,     55.5f,     52.0f,     48.5f,     45.5f,     42.5f,
                39.5f,     36.5f,     34.0f,     31.5f,     29.0f,     26.5f,
                24.5f,     22.5f,     20.5f,     19.0f,     17.5f,     15.5f,
                14.5f,     13.0f,     12.0f,     10.5f,      9.5f,      8.5f,
                 8.0f,      7.0f,      6.5f,      5.5f,      5.0f,      4.5f,
                 4.0f,      3.5f,      3.5f,      3.0f,      2.5f,      2.5f,
                 2.0f,      2.0f,      1.5f,      1.5f,      1.0f,      1.0f,
                 1.0f,      1.0f,      0.5f,      0.5f,      0.5f,      0.5f,
                 0.5f,      0.5f
        };

        // Quantizer lookup, step 1: bitrate classes
        public static readonly byte[][] QuantLutStep1 = {
	        //          32, 48, 56, 64, 80, 96,112,128,160,192,224,256,320,384 <- bitrate
	        new byte[] { 0,  0,  1,  1,  1,  2,  2,  2,  2,  2,  2,  2,  2,  2 }, // mono
	        //          16, 24, 28, 32, 40, 48, 56, 64, 80, 96,112,128,160,192 <- bitrate / chan
	        new byte[] { 0,  0,  0,  0,  0,  0,  1,  1,  1,  2,  2,  2,  2,  2 } // stereo
        };

        // Quantizer lookup, step 2: bitrate class, sample rate -> B2 table idx, sblimit
        public const byte QuantTabA = 27 | 64;   // Table 3-B.2a: high-rate, sblimit = 27
        public const byte QuantTabB = 30 | 64;   // Table 3-B.2b: high-rate, sblimit = 30
        public const byte QuantTabC = 8;           // Table 3-B.2c:  low-rate, sblimit =  8
        public const byte QuantTabD = 12;          // Table 3-B.2d:  low-rate, sblimit = 12


        public static readonly byte[][] QuantLutStep2 = {
	        //           44.1 kHz,  48 kHz,    32 kHz
	        new byte[] { QuantTabC, QuantTabC, QuantTabD }, // 32 - 48 kbit/sec/ch
	        new byte[] { QuantTabA, QuantTabA, QuantTabA }, // 56 - 80 kbit/sec/ch
	        new byte[] { QuantTabB, QuantTabA, QuantTabB }  // 96+	   kbit/sec/ch
        };

        // Quantizer lookup, step 3: B2 table, subband -> nbal, row index
        // (upper 4 bits: nbal, lower 4 bits: row index)
        public static readonly byte[][] QuantLutStep3 = {
            // Low-rate table (3-B.2c and 3-B.2d)
            new byte[] {
                0x44,0x44,
                0x34,0x34,0x34,0x34,0x34,0x34,0x34,0x34,0x34,
            },
	        // High-rate table (3-B.2a and 3-B.2b)
	        new byte[] {
                0x43,0x43,0x43,
                0x42,0x42,0x42,0x42,0x42,0x42,0x42,0x42,
                0x31,0x31,0x31,0x31,0x31,0x31,0x31,0x31,0x31,0x31,0x31,0x31,
                0x20,0x20,0x20,0x20,0x20,0x20,0x20
            },
	        // MPEG-2 LSR table (B.2 in ISO 13818-3)
	        new byte[] {
                0x45,0x45,0x45,0x45,
                0x34,0x34,0x34,0x34,0x34,0x34,0x34,
                0x24,0x24,0x24,0x24,0x24,0x24,0x24,0x24,0x24,0x24,
                0x24,0x24,0x24,0x24,0x24,0x24,0x24,0x24,0x24
            }
        };

        // Quantizer lookup, step 4: table row, allocation[] value -> quant table index
        public static readonly byte[][] QuantLutStep4 = {
            new byte[] { 0, 1, 2, 17 },
            new byte[] { 0, 1, 2,  3, 4, 5, 6, 17 },
            new byte[] { 0, 1, 2,  3, 4, 5, 6,  7,  8,  9, 10, 11, 12, 13, 14, 17 },
            new byte[] { 0, 1, 3,  5, 6, 7, 8,  9, 10, 11, 12, 13, 14, 15, 16, 17 },
            new byte[] { 0, 1, 2,  4, 5, 6, 7,  8,  9, 10, 11, 12, 13, 14, 15, 17 },
            new byte[] { 0, 1, 2,  3, 4, 5, 6,  7,  8,  9, 10, 11, 12, 13, 14, 15 }
        };

        public static readonly QuantizerSpec[] QuantTab = {
            new(     3, true,   5 ),  //  1
	        new(     5, true,   7 ),  //  2
	        new(     7, false,  3 ),  //  3
	        new(     9, true,  10 ),  //  4
	        new(    15, false,  4 ),  //  5
	        new(    31, false,  5 ),  //  6
	        new(    63, false,  6 ),  //  7
	        new(   127, false,  7 ),  //  8
	        new(   255, false,  8 ),  //  9
	        new(   511, false,  9 ),  // 10
	        new(  1023, false, 10 ),  // 11
	        new(  2047, false, 11 ),  // 12
	        new(  4095, false, 12 ),  // 13
	        new(  8191, false, 13 ),  // 14
	        new( 16383, false, 14 ),  // 15
	        new( 32767, false, 15 ),  // 16
	        new( 65535, false, 16 )   // 17
        };
    }
}
