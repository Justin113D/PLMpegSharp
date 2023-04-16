using PLMpegSharp.Container;

namespace PLMpegSharp
{
    /// <summary>
    /// High level MPEG1 Player
    /// </summary>
    public class Player
    {
        #region Private Fields/Properties

        private readonly Demux _demux;
        private bool _has_decoders;

        private bool _videoEnabled;
        private PacketStartCode _videoPacketType;
        private DataBuffer? _videoBuffer;
        private VideoDecoder? _videoDecoder;

        private bool _audioEnabled;
        private int _audioStreamIndex;
        private PacketStartCode _audioPacketType;
        private DataBuffer? _audioBuffer;
        private AudioDecoder? _audioDecoder;

        private bool HasDecoders
        {
            get
            {
                if (!_has_decoders)
                    InitDecoders();
                return _has_decoders;
            }
        }

        private AudioDecoder? AudioDecoder
            => HasDecoders ? _audioDecoder : null;

        private VideoDecoder? VideoDecoder
            => HasDecoders ? _videoDecoder : null;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether we have headers on all avialable streams and we can accurately
        /// report the number of video/audio streams, video dimensions, framerate and
        /// audio samplerate. <br/>
        /// Returns false if the file is not an MPEG-PS file or - when not using a
        /// file as source - when not enough data is available yet
        /// </summary>
        public bool HasHeaders
        {
            get
            {
                if (!_demux.HasHeaders)
                    return false;

                if (!HasDecoders)
                    return false;

                if (_videoDecoder?.HasHeader != true
                    || _audioDecoder?.HasHeader != true)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Current internal time in seconds
        /// </summary>
        public double Time { get; private set; }

        /// <summary>
        /// Video duration of the underlying source in seconds
        /// </summary>
        public double Duration
            => _demux.GetDuration(PacketStartCode.VideoFirst);

        /// <summary>
        /// Whether the playback should loop. Default false
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Whether the file has ended. <br/>
        /// If looping is enabled, this will always return false.
        /// </summary>
        public bool HasEnded { get; private set; }


        /// <summary>
        /// Whether audio decoding is enabled. Default true.
        /// </summary>
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                _audioEnabled = true;
                if (!value)
                {
                    _audioPacketType = PacketStartCode.Invalid;
                    return;
                }

                _audioPacketType = AudioDecoder == null
                    ? PacketStartCode.AudioFirst + _audioStreamIndex
                    : PacketStartCode.Invalid;

            }
        }

        /// <summary>
        /// Callback for decoded audio samples used with <see cref="Decode(double)"/>. <br/>
        /// If no callback is set, audio data will be ignored and not be decoded.
        /// </summary>
        public event AudioDecodeCallback? AudioDecodeCallback;

        /// <summary>
        /// Number of audio streams (0--4) reported in the system header.
        /// </summary>
        public int NumAudioStreams
            => _demux.NumAudioStreams;

        /// <summary>
        /// The desired audio stream (0--3). Default 0.
        /// </summary>
        public int AudioStream
        {
            get => _audioStreamIndex;
            set
            {
                if (value < 0 || value > 3)
                    throw new IndexOutOfRangeException("Expecting: 0 <= Audio stream <= 3");

                _audioStreamIndex = value;

                // Update audio packet type
                AudioEnabled = _audioEnabled;
            }
        }

        /// <summary>
        /// Samplerate of the audiostream in samples per second (SPS)
        /// </summary>
        public int Samplerate
            => AudioDecoder?.Samplerate ?? 0;

        /// <summary>
        /// The audio lead time in seconds - the time in which audio samples
        /// are decoded in advance (or behind) the video decode time. <br/>
        /// Typically this should be set to the duration of the buffer of the audio API that you use
        /// for output. E.g. for SDL2: (SDL_AudioSpec.samples / samplerate)
        /// </summary>
        public double AudioLeadTime { get; set; }


        /// <summary>
        /// Whether video decoding is enabled. Default true.
        /// </summary>
        public bool VideoEnabled
        {
            get => _videoEnabled;
            set
            {
                _videoEnabled = true;
                if (!value)
                {
                    _videoPacketType = PacketStartCode.Invalid;
                    return;
                }

                _videoPacketType = VideoDecoder != null
                    ? PacketStartCode.VideoFirst
                    : PacketStartCode.Invalid;
            }
        }

        /// <summary>
        /// Callback for decoded video frames used with <see cref="Decode(double)"/>. <br/>
        /// If no callback is set, video data will be ignored and not be decoded.
        /// </summary>
        public event VideoDecodeCallback? VideoDecodeCallback;

        /// <summary>
        /// Number of video streams (0--1) reported in the system header.
        /// </summary>
        public int NumVideoStreams
            => _demux.NumVideoStreams;

        /// <summary>
        /// Display width of the video stream
        /// </summary>
        public int Width
            => VideoDecoder?.Width ?? 0;

        /// <summary>
        /// Display height of the video stream
        /// </summary>
        public int Height
            => VideoDecoder?.Height ?? 0;

        /// <summary>
        /// Framerate of the video stream in frames per second (FPS)
        /// </summary>
        public double Framerate
            => VideoDecoder?.Framerate ?? 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Create an MPEG1 Player with a databuffer as source.
        /// </summary>
        /// <param name="buffer"></param>
        private Player(DataBuffer buffer)
        {
            _demux = new(buffer);
            _videoEnabled = true;
            _audioEnabled = true;
            _audioPacketType = PacketStartCode.Invalid;
            _videoPacketType = PacketStartCode.Invalid;
            InitDecoders();
        }

        /// <summary>
        /// Create an MPEG1 Player with a filename. Throws error if file cannot be opened.
        /// </summary>
        /// <param name="filename">Path to the file to open</param>
        public Player(string filename)
            : this(DataBuffer.CreateWithFilename(filename)) { }

        /// <summary>
        /// Create an MPEG1 Player with a filestream.
        /// </summary>
        /// <param name="file">Filestream that contains the filedata</param>
        /// <param name="closeWhenDone">Close filestream when done</param>
        public Player(FileStream file, bool closeWhenDone)
            : this(DataBuffer.CreateWithFile(file, closeWhenDone)) { }

        /// <summary>
        /// Create an MPEG1 Player using byte data. <br/>
        /// This assumes the whole file is contained in the data. <br/>
        /// The memory is not copied.
        /// </summary>
        /// <param name="bytes">File memory byte data</param>
        public Player(byte[] bytes)
            : this(DataBuffer.CreateWithMemory(bytes)) { }

        #endregion

        #region Private Methods

        private void InitDecoders()
        {
            if (!_demux.HasHeaders)
                return;

            if (_demux.NumVideoStreams > 0)
            {
                if (_videoEnabled)
                {
                    _videoPacketType = PacketStartCode.VideoFirst;
                }

                _videoBuffer = DataBuffer.CreateWithCapacity();
                _videoBuffer.LoadCallback = ReadVideoPacket;
            }

            if (_demux.NumAudioStreams > 0)
            {
                if (_audioEnabled)
                {
                    _audioPacketType = PacketStartCode.AudioFirst + _audioStreamIndex;
                }

                _audioBuffer = DataBuffer.CreateWithCapacity();
                _audioBuffer.LoadCallback = ReadAudioPacket;
            }

            if (_videoBuffer != null)
                _videoDecoder = new(_videoBuffer);

            if (_audioBuffer != null)
                _audioDecoder = new(_audioBuffer);

            _has_decoders = true;
        }

        private void HandleEnd()
        {
            if (Loop)
            {
                Rewind();
            }
            else
            {
                HasEnded = true;
            }
        }

        private void ReadVideoPacket(DataBuffer buffer)
            => ReadPackets(_videoPacketType);

        private void ReadAudioPacket(DataBuffer buffer)
            => ReadPackets(_audioPacketType);

        private void ReadPackets(PacketStartCode requestedType)
        {
            Packet? packet = _demux.Decode();
            while (packet != null)
            {
                if (packet.Type == _videoPacketType)
                {
                    _videoBuffer?.Write(packet.Data);
                }
                else if (packet.Type == _audioPacketType)
                {
                    _audioBuffer?.Write(packet.Data);
                }

                if (packet.Type == requestedType)
                    return;

                packet = _demux.Decode();
            }

            if (_demux.HasEnded)
            {
                _videoBuffer?.SignalEnd();
                _audioBuffer?.SignalEnd();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Advance the internal timer by seconds and decode video/audio up to this time. <br/>
        /// This will call <see cref="VideoDecodeCallback"/> and <see cref="AudioDecodeCallback"/> 
        /// any number of times. <br/>
        /// A frame-skip is not implemented, i.e. everything up to current time will be decoded.
        /// </summary>
        /// <param name="tick">Time in seconds to advance the internal timer</param>
        public void Decode(double tick)
        {
            if (!HasDecoders)
                return;

            bool decode_video = (VideoDecodeCallback != null && _videoPacketType != PacketStartCode.Invalid);
            bool decode_audio = (AudioDecodeCallback != null && _audioPacketType != PacketStartCode.Invalid);

            if (!decode_video && !decode_audio)
            {
                // Nothing to do here
                return;
            }

            bool did_decode;
            bool decode_video_failed = false;
            bool decode_audio_failed = false;

            double video_target_time = Time + tick;
            double audio_target_time = Time + tick + AudioLeadTime;

            do
            {
                did_decode = false;

                if (decode_video && _videoDecoder?.Time < video_target_time)
                {
                    Frame? frame = _videoDecoder.Decode();
                    if (frame != null)
                    {
                        VideoDecodeCallback?.Invoke(this, frame);
                        did_decode = true;
                    }
                    else
                    {
                        decode_video_failed = true;
                    }
                }

                if (decode_audio && _audioDecoder?.Time < audio_target_time)
                {
                    Samples? samples = _audioDecoder.Decode();
                    if (samples != null)
                    {
                        AudioDecodeCallback?.Invoke(this, samples);
                        did_decode = true;
                    }
                    else
                    {
                        decode_audio_failed = true;
                    }
                }
            } while (did_decode);

            // Did all sources we wanted to decode fail and the demuxer is at the end?
            if ((!decode_video || decode_video_failed)
                && (!decode_audio || decode_audio_failed)
                && _demux.HasEnded)
            {
                HandleEnd();
                return;
            }

            Time += tick;
        }

        /// <summary>
        /// Decode and return one video frame. Returns null if no frame could be decoded
        /// (either because the source ended or data is corrupt). <br/>
        /// If you only want to decode video, you should disable <see cref="AudioEnabled"/>. <br/>
        /// The returned <see cref="Frame"/> is valid until the next call to <see cref="DecodeVideo"/>.
        /// </summary>
        public Frame? DecodeVideo()
        {
            if (VideoDecoder == null || _videoPacketType == PacketStartCode.Invalid)
                return null;

            Frame? frame = VideoDecoder.Decode();
            if (frame != null)
            {
                Time = frame.Time;
            }
            else if (_demux.HasEnded)
            {
                HandleEnd();
            }
            return frame;
        }

        /// <summary>
        /// Decode and return one audio frame. Returns null if no frame could be decoded
        /// (either because the source ended or data is corrupt). <br/>
        /// If you only want to decode audio, you should disable <see cref="VideoEnabled"/>. <br/>
        /// The returned <see cref="Samples"/> is valid until the next call to <see cref="DecodeAudio"/>.
        /// </summary>
        public Samples? DecodeAudio()
        {
            if (AudioDecoder == null || _audioPacketType == PacketStartCode.Invalid)
                return null;

            Samples? samples = AudioDecoder.Decode();
            if (samples != null)
            {
                Time = samples.Time;
            }
            else if (_demux.HasEnded)
            {
                HandleEnd();
            }
            return samples;
        }

        /// <summary>
        /// Rewind all buffers back to the beginning
        /// </summary>
        public void Rewind()
        {
            VideoDecoder?.Rewind();
            AudioDecoder?.Rewind();
            _demux.Rewind();
            Time = 0;
        }

        /// <summary>
        /// Seek to the specified time, clamped between 0 -- <see cref="Duration"/>. This can only be
        /// used when the underlying <see cref="DataBuffer"/> is seekable, i.e. for files, fixed
        /// memory buffers or for appending buffers. <br/>
        /// If seeking succeeds, this function will call <see cref="VideoDecodeCallback"/>
        /// exactly once with the target frame. <br/>
        /// If <see cref="AudioEnabled"/> is true, it will also call
        /// the <see cref="AudioDecodeCallback"/> any number of times, until the <see cref="AudioLeadTime"/> is
        /// satisfied.
        /// </summary>
        /// <param name="time">Time in seconds to seek to</param>
        /// <param name="seekExact">seek to the exact time, otherwise
        /// seek to the last intra frame just before the desired time <br/>
        /// Exact seeking can be slow, because all frames up to the seeked one have to be decoded on top of
        /// the previous intra frame.
        /// </param>
        /// <returns>Whether seeking succeeded</returns>
        public bool Seek(double time, bool seekExact)
        {
            Frame? frame = SeekFrame(time, seekExact);

            if (frame == null)
                return false;

            VideoDecodeCallback?.Invoke(this, frame);

            // If audio is not enabled we are done here.
            if (AudioDecoder == null || _audioPacketType == PacketStartCode.Invalid)
                return false;

            // Sync up Audio. This demuxes more packets until the first audio packet
            // with a PTS greater than the current time is found. plm_decode() is then
            // called to decode enough audio data to satisfy the audio_lead_time.

            double start_time = _demux.GetStartTime(_videoPacketType);
            AudioDecoder.Rewind();

            Packet? packet = _demux.Decode();
            while (packet != null)
            {
                if (packet.Type == _videoPacketType)
                {
                    _videoBuffer?.Write(packet.Data);
                }
                else if (packet.Type == _audioPacketType && packet.PTS - start_time > Time)
                {
                    AudioDecoder.Time = packet.PTS - start_time;
                    _audioBuffer?.Write(packet.Data);
                    Decode(0);
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Similar to <see cref="Seek(double, bool)"/>, but will not call <see cref="VideoDecodeCallback"/>,
        /// <see cref="AudioDecodeCallback"/> or make any attempts to sync audio. <br/>
        /// Returns the found <see cref="Frame"/> or null if no frame could be found.
        /// </summary>
        /// <param name="time">Time in seconds to seek to</param>
        /// <param name="seekExact">seek to the exact time, otherwise
        /// seek to the last intra frame just before the desired time <br/>
        /// Exact seeking can be slow, because all frames up to the seeked one have to be decoded on top of
        /// the previous intra frame.
        /// </param>
        /// <returns>The seeked frame</returns>
        public Frame? SeekFrame(double time, bool seekExact)
        {
            if (VideoDecoder == null)
                return null;

            PacketStartCode type = _videoPacketType;

            double start_time = _demux.GetStartTime(type);
            double duration = _demux.GetDuration(type);
            time = double.Clamp(time, 0, duration);

            Packet? packet = _demux.Seek(time, type, true);
            if (packet == null)
                return null;

            // Disable writing to the audio buffer while decoding video
            PacketStartCode previousAudioPacketType = _audioPacketType;
            _audioPacketType = PacketStartCode.Invalid;

            // Clear video buffer and decode the found packet
            VideoDecoder.Rewind();
            VideoDecoder.Time = packet.PTS - start_time;
            _videoBuffer?.Write(packet.Data);
            Frame? frame = VideoDecoder.Decode();

            // If we want to seek to an exact frame, we have to decode all frames
            // on top of the intra frame we just jumped to.
            if (seekExact)
            {
                while (frame?.Time < time)
                {
                    frame = VideoDecoder.Decode();
                }
            }

            // Enable writing to the audio buffer again?
            _audioPacketType = previousAudioPacketType;

            if (frame != null)
            {
                Time = frame.Time;
            }

            HasEnded = false;
            return frame;
        }

        #endregion

    }
}
