namespace PLMpegSharp.Container
{
    /// <summary>
    /// Decoded Audio Samples
    /// </summary>
    public class Samples
    {
        public const int AudioSamplesPerFrame = 1152;

        /// <summary>
        /// Timestamp in seconds
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// Normalized [-1, 1] left audio channel
        /// </summary>
        public float[] Left { get; }

        /// <summary>
        /// Normalized [-1, 1] right audio channel
        /// </summary>
        public float[] Right { get; }

        internal Samples()
        {
            Time = 0.0;
            Left = new float[AudioSamplesPerFrame];
            Right = new float[AudioSamplesPerFrame];
        }
    }
}
