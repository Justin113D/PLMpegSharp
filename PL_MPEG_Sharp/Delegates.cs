using PLMpegSharp.Container;

namespace PLMpegSharp
{
    /// <summary>
    /// Callback function type for decoded audio samples
    /// </summary>
    /// <param name="player">Player calling the callback</param>
    /// <param name="samples">Decoded samples</param>
    public delegate void AudioDecodeCallback(Player player, Samples? samples);

    /// <summary>
    /// Callback function type for decoded video frames
    /// </summary>
    /// <param name="player">Player calling the callback</param>
    /// <param name="frame">Decoded frame</param>
    public delegate void VideoDecodeCallback(Player player, Frame? frame);

    /// <summary>
    /// Callback function for when the buffer needs more data
    /// </summary>
    /// <param name="buffer"></param>
    public delegate void BufferLoadCallback(DataBuffer buffer);
}
