namespace PLMpegSharp.LUT
{
    internal struct VLC
    {
        public short index;
        public short value;

        public VLC(short index, short value)
        {
            this.index = index;
            this.value = value;
        }
    }

    internal struct UVLC
    {
        public short index;
        public ushort value;

        public UVLC(short index, ushort value)
        {
            this.index = index;
            this.value = value;
        }
    }

    internal struct QuantizerSpec
    {
        public ushort levels;
        public bool group;
        public byte bits;

        public QuantizerSpec(ushort levels, bool group, byte bits)
        {
            this.levels = levels;
            this.group = group;
            this.bits = bits;
        }
    }
}
