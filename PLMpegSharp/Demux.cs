using PLMpegSharp.Container;

namespace PLMpegSharp
{
    /// <summary>
    /// MPEG-PS Packet demuxer
    /// </summary>
    public class Demux
    {
        #region Private Fields

        private readonly DataBuffer _buffer;
        private readonly Packet _currentPacket;
        private readonly Packet _nextPacket;

        private bool _hasHeaders;
        private bool _hasPackHeader;
        private bool _hasSystemHeader;

        private int _numAudioStreams;
        private int _numVideoStreams;
        private nuint _lastFileSize;
        private double _lastDecodedPTS;
        private double _startTime;
        private double _duration;
        private PacketStartCode _startCode;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether pack and system headers have been found. <br/>
        /// This will attempt to read the headers if non are present yet.
        /// </summary>
        public bool HasHeaders
            => _hasHeaders || DecodeHeaders();

        /// <summary>
        /// Whether the file has ended. This will be cleared on <see cref="Seek(double, PacketStartCode, bool)"/> or <see cref="Rewind"/>.
        /// </summary>
        public bool HasEnded
            => _buffer.HasEnded;

        /// <summary>
        /// The number of audio streams found in the system header. <br/>
        /// This will attempt to read the system header if non is present yet.
        /// </summary>
        public int NumAudioStreams
            => HasHeaders ? _numAudioStreams : 0;

        /// <summary>
        /// The number of video streams found in the system header. <br/>
        /// This will attempt to read the system header if non is present yet.
        /// </summary>
        public int NumVideoStreams
            => HasHeaders ? _numVideoStreams : 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Create a demuxer with a <see cref="DataBuffer"/> as source. This will also attempt to read
        /// the pack and system headers from the buffer.
        /// </summary>
        /// <param name="buffer"></param>
        public Demux(DataBuffer buffer)
        {
            _buffer = buffer;

            _startTime = Packet.InvalidTS;
            _duration = Packet.InvalidTS;
            _startCode = PacketStartCode.Invalid;

            _currentPacket = new(_buffer);
            _nextPacket = new(_buffer);

            DecodeHeaders();
        }

        #endregion

        #region Private methodMethods

        private bool DecodeHeaders()
        {
            if (!_hasPackHeader)
            {
                if (_startCode != PacketStartCode.Pack
                    && _buffer.FindStartCode(PacketStartCode.Pack) == PacketStartCode.Invalid)
                {
                    return false;
                }

                _startCode = PacketStartCode.Pack;
                if (!_buffer.Has(64))
                    return false;
                _startCode = PacketStartCode.Invalid;

                if (_buffer.Read(4) != 2)
                    return false;

                DecodeTime(); // system ref time
                _buffer.Skip(1);
                _buffer.Skip(22); // mux_rate * 50
                _buffer.Skip(1);

                _hasPackHeader = true;
            }

            if (!_hasSystemHeader)
            {
                if (_startCode != PacketStartCode.System
                    && _buffer.FindStartCode(PacketStartCode.System) == PacketStartCode.Invalid)
                {
                    return false;
                }

                _startCode = PacketStartCode.System;
                if (!_buffer.Has(56))
                    return false;
                _startCode = PacketStartCode.Invalid;

                _buffer.Skip(16); // header_length
                _buffer.Skip(24); // rate bound
                _numAudioStreams = (int)_buffer.Read(6);
                _buffer.Skip(5); // misc flags
                _numVideoStreams = (int)_buffer.Read(5);

                _hasSystemHeader = true;
            }

            _hasHeaders = true;
            return true;
        }

        private void BufferSeek(nuint pos)
        {
            _buffer.Seek(pos);
            _currentPacket.Length = 0;
            _nextPacket.Length = 0;
            _startCode = PacketStartCode.Invalid;
        }

        private double DecodeTime()
        {
            nuint clock = _buffer.Read(3) << 30;
            _buffer.Skip(1);
            clock |= _buffer.Read(15) << 15;
            _buffer.Skip(1);
            clock |= _buffer.Read(15);
            _buffer.Skip(1);
            return clock / 90000.0;
        }

        private Packet? DecodePacket(PacketStartCode type)
        {
            if (!_buffer.Has(16 << 3))
                return null;

            _startCode = PacketStartCode.Invalid;

            _nextPacket.Type = type;
            _nextPacket.Length = _buffer.Read(16);
            _nextPacket.Length -= (nuint)_buffer.SkipBytes(0xFF); // stuffing

            // skip P-STD
            if (_buffer.Read(2) == 1)
            {
                _buffer.Skip(16);
                _nextPacket.Length -= 2;
            }

            nuint pts_dts_marker = _buffer.Read(2);
            switch (pts_dts_marker)
            {
                case 0:
                    _nextPacket.PTS = Packet.InvalidTS;
                    _buffer.Skip(4);
                    _nextPacket.Length -= 1;
                    break;
                case 2:
                    _lastDecodedPTS = _nextPacket.PTS = DecodeTime();
                    _nextPacket.Length -= 5;
                    break;
                case 3:
                    _lastDecodedPTS = _nextPacket.PTS = DecodeTime();
                    _buffer.Skip(40); // skip dits
                    _nextPacket.Length -= 10;
                    break;
                default:
                    return null;
            }

            return GetPacket();
        }

        private Packet? GetPacket()
        {
            if (!_buffer.Has(_nextPacket.Length << 3))
                return null;

            _currentPacket.StartIndex = _buffer.BitIndex >> 3;
            _currentPacket.Length = _nextPacket.Length;
            _currentPacket.Type = _nextPacket.Type;
            _currentPacket.PTS = _nextPacket.PTS;

            _nextPacket.Length = 0;
            return _currentPacket;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Rewinds the internal buffer. See <see cref="DataBuffer.Rewind"/>.
        /// </summary>
        public void Rewind()
        {
            _buffer.Rewind();
            _currentPacket.Length = 0;
            _nextPacket.Length = 0;
            _startCode = PacketStartCode.Invalid;
        }

        /// <summary>
        /// Seek to a packet of the specified type with a PTS just before specified time. <br/>
        /// If <paramref name="forceIntra"/> is true, only packets containing an intra frame will be
        /// considered - this only makes sense when the type is <see cref="PacketStartCode.VideoFirst"/>. <br/>
        /// Note that the specified time is considered 0-based, regardless of the first
        /// PTS in the data source.
        /// </summary>
        /// <param name="seekTime">Time to seek to</param>
        /// <param name="type">Packet type to seek to</param>
        /// <param name="forceIntra">Only packets containing an intra frame will be considered</param>
        /// <returns></returns>
        public Packet? Seek(double seekTime, PacketStartCode type, bool forceIntra)
        {
            if (!HasHeaders)
                return null;

            double duration = GetDuration(type);
            nuint fileSize = _buffer.Size;
            nuint byteRate = (nuint)(fileSize / duration);

            double curTime = _lastDecodedPTS;
            double scanSpan = 1;

            if (seekTime > duration)
            {
                seekTime = duration;
            }
            else if (seekTime < 0)
            {
                seekTime = 0;
            }
            seekTime += _startTime;

            for (int retry = 0; retry < 32; retry++)
            {
                bool foundPacketWithPTS = false;
                bool foundPacketInRange = false;
                nuint lastValidPacketStart = nuint.MaxValue;
                double firstPacketTime = Packet.InvalidTS;

                nuint curPos = _buffer.Tell;

                nuint offset = (nuint)(seekTime - curTime - scanSpan) * byteRate;
                nuint seekPos = curPos + offset;
                if (seekPos < 0)
                {
                    seekPos = 0;
                }
                else if (seekPos > fileSize - 256)
                {
                    seekPos = fileSize - 256;
                }

                BufferSeek(seekPos);

                while (_buffer.FindStartCode(type) != PacketStartCode.Invalid)
                {
                    nuint packetStart = _buffer.Tell;
                    Packet? packet = DecodePacket(type);

                    if (packet == null || packet.PTS == Packet.InvalidTS)
                    {
                        continue;
                    }

                    if (packet.PTS > seekTime
                        || packet.PTS < seekTime - scanSpan)
                    {
                        byteRate = (nuint)((seekPos - curPos) / (packet.PTS - curTime));
                        curTime = packet.PTS;
                        break;
                    }

                    if (!foundPacketInRange)
                    {
                        foundPacketInRange = true;
                        firstPacketTime = packet.PTS;
                    }

                    if (forceIntra)
                    {
                        for (int i = 0; i < (uint)packet.Length - 6; i++)
                        {
                            if (packet.Data[i] == 0
                                && packet.Data[i + 1] == 0
                                && packet.Data[i + 2] == 1
                                && packet.Data[i + 3] == 0)
                            {
                                if ((packet.Data[i + 5] & 0x38) == 8)
                                {
                                    lastValidPacketStart = packetStart;
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        lastValidPacketStart = packetStart;
                    }
                }

                if (lastValidPacketStart != nuint.MaxValue)
                {
                    BufferSeek(lastValidPacketStart);
                    return DecodePacket(type);
                }
                else if (foundPacketInRange)
                {
                    scanSpan *= 2;
                    seekTime = firstPacketTime;
                }
                else if (!foundPacketWithPTS)
                {
                    byteRate = (nuint)((seekPos - curPos) / (duration - curTime));
                    curTime = duration;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the PTS of the first packet of this type. <br/>
        /// Returns <see cref="Packet.InvalidTS"/> if no packet of this type can be found.
        /// </summary>
        /// <param name="type">Type of the first packet to get the PTS of</param>
        /// <returns></returns>
        public double GetStartTime(PacketStartCode type)
        {
            if (_startTime != Packet.InvalidTS)
                return _startTime;

            nuint previousPos = _buffer.Tell;
            PacketStartCode previousStartCode = _startCode;

            Rewind();
            do
            {
                Packet? packet = Decode();
                if (packet == null)
                    break;
                if (packet.Type == type)
                {
                    _startTime = packet.PTS;
                }
            }
            while (_startTime == Packet.InvalidTS);

            BufferSeek(previousPos);
            _startCode = previousStartCode;
            return _startTime;
        }

        /// <summary>
        /// Get the duration for the specified packet type - i.e. the span between the
        /// the first PTS and the last PTS in the data source. <br/>
        /// This only makes sense when the underlying data source is a file or fixed memory.
        /// </summary>
        /// <param name="type">Type of the packet type to get the duration of</param>
        /// <returns></returns>
        public double GetDuration(PacketStartCode type)
        {
            nuint fileSize = _buffer.Size;
            if (_duration != Packet.InvalidTS
                && _lastFileSize == fileSize)
            {
                return _duration;
            }

            nuint previousPos = _buffer.Tell;
            PacketStartCode previousStartCode = _startCode;

            // Find last video PTS. Start searching 64kb from the end and go further
            // back if needed.
            long startRange = 64 * 1024;
            long maxRange = 4096 * 1024;

            for (long range = startRange; range <= maxRange; range *= 2)
            {
                long seekPos = (long)fileSize - range;
                if (seekPos < 0)
                {
                    seekPos = 0;
                    range = maxRange; // Make sure to bail after this round
                }

                BufferSeek((nuint)seekPos);
                _currentPacket.Length = 0;

                double lastPTS = Packet.InvalidTS;
                Packet? packet = Decode();
                while (packet != null)
                {
                    if (packet.PTS != Packet.InvalidTS && packet.Type == type)
                    {
                        lastPTS = packet.PTS;
                    }
                    packet = Decode();
                }

                if (lastPTS != Packet.InvalidTS)
                {
                    _duration = lastPTS - GetStartTime(type);
                    break;
                }
            }

            BufferSeek(previousPos);
            _startCode = previousStartCode;
            _lastFileSize = fileSize;
            return _duration;
        }

        /// <summary>
        /// Decode and return the next packet. The returned <see cref="Packet"/> is valid until
        /// the next call to <see cref="Decode"/>.
        /// </summary>
        /// <returns></returns>
        public Packet? Decode()
        {
            if (!HasHeaders)
                return null;

            if (_currentPacket.Length != 0)
            {
                nuint bitsTillNextPacket = _currentPacket.Length << 3;
                if (!_buffer.Has(bitsTillNextPacket))
                    return null;

                _buffer.Skip(bitsTillNextPacket);
                _currentPacket.Length = 0;
            }

            if (_nextPacket.Length != 0)
            {
                return GetPacket();
            }

            if (_startCode != PacketStartCode.Invalid)
            {
                return DecodePacket(_startCode);
            }

            do
            {
                _startCode = _buffer.NextStartCode();
                if (_startCode == PacketStartCode.VideoFirst
                    || _startCode == PacketStartCode.PrivateStream1
                    || (_startCode >= PacketStartCode.AudioFirst &&
                         _startCode <= PacketStartCode.AudioFirst + 4))
                {
                    return DecodePacket(_startCode);
                }
            }
            while (_startCode != PacketStartCode.Invalid);

            return null;
        }

        #endregion
    }
}
