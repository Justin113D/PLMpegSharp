﻿using PLMpegSharp.LUT;
using System.Diagnostics.CodeAnalysis;

namespace PLMpegSharp
{
    /// <summary>
    /// Data buffer for reading file data
    /// </summary>
    public class DataBuffer
    {
        private enum Mode
        {
            File,
            FixedMemory,
            Ring,
            Append
        }

        /// <summary>
        /// The default size for buffers created from files or by the high-level API
        /// </summary>
        public const int DefaultCapacity = 128 * 1024;

        #region Private/Internal Fields/Properties

        private readonly Mode _mode;
        private readonly bool _closeWhenDone;
        private readonly FileStream? _file;

        private byte[] _bytes;
        private nuint _totalSize;
        private bool _discardReadBytes;

        [MemberNotNullWhen(true, nameof(_file))]
        private bool IsFileMode
            => _mode == Mode.File;

        internal nuint BitIndex { get; set; }
        internal ReadOnlySpan<byte> Bytes => _bytes;
        internal nuint Length { get; private set; }

        public nuint Tell
            => IsFileMode
                ? (nuint)_file.Position + (BitIndex >> 3) - Length
                : BitIndex >> 3;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the read position of the buffer is at the end and no more data is expected.
        /// </summary>
        public bool HasEnded { get; private set; }

        /// <summary>
        /// Get the total size. For files, this returns the file size. For all other
        /// types it returns the number of bytes currently in the buffer.
        /// </summary>
        public nuint Size
            => _mode == Mode.File ? _totalSize : Length;

        /// <summary>
        /// Get the number of remaining (yet unread) bytes in the buffer. This can be
        /// useful to throttle writing.
        /// </summary>
        public nuint Remaining
            => Length - (BitIndex >> 3);

        /// <summary>
        /// Callback that is called whenever the buffer needs more data
        /// </summary>
        public BufferLoadCallback? LoadCallback { get; set; }

        #endregion

        #region Constructors / Destructor

        private DataBuffer(byte[] bytes, Mode mode, bool discardReadBytes)
        {
            _bytes = bytes;
            _mode = mode;
            _discardReadBytes = discardReadBytes;
        }

        private DataBuffer(byte[] bytes, Mode mode, bool discardReadBytes, FileStream? file, bool closeWhenDone) : this(bytes, mode, discardReadBytes)
        {
            _file = file;
            _closeWhenDone = closeWhenDone;
        }

        ~DataBuffer()
        {
            if (_file != null && _closeWhenDone)
            {
                _file.Close();
            }
        }

        #endregion

        #region public Static Methods

        /// <summary>
        /// Create a buffer instance with a filename. Throws error if the file could not be opened.
        /// </summary>
        /// <param name="filename">Path to the file to open</param>
        /// <returns>The created databuffer</returns>
        public static DataBuffer CreateWithFilename(string filename)
            => CreateWithFile(new FileStream(filename, FileMode.Open, FileAccess.Read), true);

        /// <summary>
        /// Create a buffer instance with a file handle.
        /// </summary>
        /// <param name="stream">Filestream to use</param>
        /// <param name="closeWhenDone">Close the file on destruct</param>
        /// <returns>The created databuffer</returns>
        public static DataBuffer CreateWithFile(FileStream stream, bool closeWhenDone)
        {
            DataBuffer result = new(new byte[DefaultCapacity], Mode.File, true, stream, closeWhenDone)
            {
                _totalSize = (nuint)stream.Length,
            };

            result.LoadCallback = result.LoadFileCallback;
            return result;
        }

        /// <summary>
        /// Create a buffer instance with a pointer to memory as source.  <br/>
        /// This assumes the whole file is in memory. The bytes are not copied.
        /// </summary>
        /// <param name="bytes">File byte data</param>
        /// <returns>The created databuffer</returns>
        public static DataBuffer CreateWithMemory(byte[] bytes)
        {
            return new(bytes, Mode.FixedMemory, false)
            {
                Length = (nuint)bytes.Length,
                _totalSize = (nuint)bytes.Length
            };
        }

        /// <summary>
        /// Create an empty buffer with an initial capacity. The buffer will grow
        /// as needed. Data that has already been read, will be discarded.
        /// </summary>
        /// <param name="initialCapacity">Byte capacity</param>
        /// <returns>The created databuffer</returns>
        public static DataBuffer CreateWithCapacity(uint initialCapacity = DefaultCapacity)
            => new(new byte[initialCapacity], Mode.Ring, true);

        /// <summary>
        /// Create an empty buffer with an initial capacity. The buffer will grow
        /// as needed. Decoded data will *not* be discarded. This can be used when
        /// loading a file over the network, without needing to throttle the download.
        /// It also allows for seeking in the already loaded data.
        /// </summary>
        /// <param name="initialCapacity">Initial byte capacity</param>
        /// <returns>The created databuffer</returns>
        public static DataBuffer CreateForAppending(uint initialCapacity = DefaultCapacity)
            => new(new byte[initialCapacity], Mode.Append, false);

        #endregion

        #region Private/Internal Methods

        private void LoadFileCallback(DataBuffer buffer)
        {
            if (buffer._discardReadBytes)
            {
                buffer.DiscardReadBytes();
            }

            int bytesAvailable = (int)((nuint)buffer._bytes.LongLength - buffer.Length);
            int bytesRead = buffer._file?.Read(buffer._bytes, (int)buffer.Length, bytesAvailable) ?? throw new NullReferenceException();
            buffer.Length += (nuint)bytesRead;

            if (bytesRead == 0)
            {
                buffer.HasEnded = true;
            }
        }

        internal short ReadVLC(VLC[] table)
        {
            VLC state = default;
            do
            {
                state = table[(nuint)state.index + Read(1)];
            }
            while (state.index > 0);
            return state.value;
        }

        internal ushort ReadUVLC(UVLC[] table)
        {
            UVLC state = default;
            do
            {
                state = table[(nuint)state.index + Read(1)];
            }
            while (state.index > 0);
            return state.value;
        }

        internal void Seek(nuint pos)
        {
            HasEnded = false;

            if (IsFileMode)
            {
                _file.Seek((long)pos, SeekOrigin.Begin);
                BitIndex = 0;
                Length = 0;
            }
            else if (_mode == Mode.Ring)
            {
                if (pos != 0)
                    return;

                BitIndex = 0;
                Length = 0;
                _totalSize = 0;
            }
            else
            {
                BitIndex = pos << 3;
            }
        }

        internal void DiscardReadBytes()
        {
            nuint bytePos = BitIndex >> 3;
            if (bytePos == Length)
            {
                BitIndex = 0;
                Length = 0;
            }
            else if (bytePos > 0)
            {
                Array.Copy(_bytes, (long)bytePos, _bytes, 0, (long)(Length - bytePos));
                BitIndex -= bytePos << 3;
                Length -= bytePos;
            }
        }

        internal bool Has(nuint count)
        {
            if (((Length << 3) - BitIndex) >= count)
                return true;

            if (LoadCallback != null)
            {
                LoadCallback(this);

                if (((Length << 3) - BitIndex) >= count)
                    return true;
            }

            if (_totalSize != 0 && Length > _totalSize)
            {
                HasEnded = true;
            }

            return false;
        }

        internal nuint Read(nuint count)
        {
            if (!Has(count))
                return 0;

            nuint result = 0;
            while (count != 0)
            {
                nuint currentByte = _bytes[BitIndex >> 3];

                nuint remaining = 8 - (BitIndex & 7); // Remaining bits in byte
                nuint read = remaining < count ? remaining : count; // Bits in self run
                nuint shift = remaining - read;
                nuint mask = (nuint)(0xFF >> (int)(8 - read));

                result = (result << (int)read) | ((currentByte & (mask << (int)shift)) >> (int)shift);

                BitIndex += read;
                count -= read;
            }

            return result;
        }

        internal void Align()
        {
            BitIndex = (BitIndex + 7) & ~7u; // Align to next byte
        }

        internal void Skip(nuint count)
        {
            if (Has(count))
            {
                BitIndex += count;
            }
        }

        internal int SkipBytes(byte v)
        {
            Align();
            int skipped = 0;
            while (Has(8) && _bytes[BitIndex >> 3] == v)
            {
                BitIndex += 8;
                skipped++;
            }
            return skipped;
        }

        internal PacketStartCode NextStartCode()
        {
            Align();

            while (Has(5 << 3))
            {
                nuint byteIndex = BitIndex >> 3;
                if (_bytes[byteIndex] == 0
                    && _bytes[byteIndex + 1] == 0
                    && _bytes[byteIndex + 2] == 1)
                {
                    BitIndex = (byteIndex + 4) << 3;
                    return (PacketStartCode)_bytes[byteIndex + 3];
                }
                BitIndex += 8;
            }
            return PacketStartCode.Invalid;
        }

        internal PacketStartCode FindStartCode(PacketStartCode code)
        {
            while (true)
            {
                PacketStartCode current = NextStartCode();
                if (current == code || current == PacketStartCode.Invalid)
                {
                    return current;
                }
            }
        }

        internal PacketStartCode HasStartCode(PacketStartCode code)
        {
            nuint previousBitIndex = BitIndex;
            bool previousDoDiscardReadBytes = _discardReadBytes;

            _discardReadBytes = false;
            PacketStartCode current = FindStartCode(code);

            BitIndex = previousBitIndex;
            _discardReadBytes = previousDoDiscardReadBytes;

            return current;
        }

        internal bool PeekNonZero(nuint bitCount)
        {
            if (!Has(bitCount))
            {
                return false;
            }

            nuint val = Read(bitCount);
            BitIndex -= bitCount;
            return val != 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Copy data into the buffer. If the data to be written is larger than the
        /// available space, the buffer will resize the array with a larger capacity. <br/>
        /// Returns the number of bytes written. This will always be the same as the
        /// passed in <see cref="ReadOnlySpan{T}.Length"/>, <br/>
        /// except when the buffer was created using <see cref="CreateWithMemory(byte[])"/> 
        /// for which this method is forbidden.
        /// </summary>
        /// <param name="bytes">Bytes to write into the buffer</param>
        /// <returns></returns>
        public nuint Write(ReadOnlySpan<byte> bytes)
        {
            if (_mode == Mode.FixedMemory)
                return 0;

            if (_discardReadBytes)
            {
                // This should be a ring buffer, but instead it just shifts all unread
                // data to the beginning of the buffer and appends new data at the end.
                // Seems to be good enough.

                DiscardReadBytes();
                if (_mode == Mode.Ring)
                {
                    _totalSize = 0;
                }
            }

            // Do we have to resize to fit the new data?
            nuint bytesAvailable = (nuint)_bytes.LongLength - Length;
            if (bytesAvailable < (nuint)bytes.Length)
            {
                nuint newSize = (nuint)_bytes.LongLength;
                do
                {
                    newSize *= 2;
                }
                while (newSize - Length < (nuint)bytes.Length);

                Array.Resize(ref _bytes, (int)newSize);
            }

            bytes.CopyTo(_bytes.AsSpan((int)Length));
            Length += (nuint)bytes.Length;
            HasEnded = false;
            return (nuint)bytes.Length;
        }

        /// <summary>
        /// Mark the current byte length as the end of this buffer and signal that no
        /// more data is expected to be written to it. <br/>
        /// This function should be called just after the last <see cref="Write(ReadOnlySpan{byte})"/>. <br/>
        /// For buffers created using <see cref="CreateWithCapacity(uint)"/>, this is cleared on a <see cref="Rewind"/>.
        /// </summary>
        public void SignalEnd()
            => _totalSize = Length;

        /// <summary>
        /// Rewind the buffer back to the beginning. When loading from a file handle,
        /// this also seeks to the beginning of the file.
        /// </summary>
        public void Rewind()
            => Seek(0);

        #endregion
    }
}
