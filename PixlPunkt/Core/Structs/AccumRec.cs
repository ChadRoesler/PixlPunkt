namespace PixlPunkt.Core.Structs
{
    /// <summary>
    /// Accumulation record tracking pixel state during a stroke.
    /// </summary>
    public struct AccumRec
    {
        /// <summary>Original pixel value before any modification in this stroke.</summary>
        public uint before;

        /// <summary>Current pixel value after modifications.</summary>
        public uint after;

        /// <summary>Maximum effective alpha applied to this pixel during the stroke.</summary>
        public byte maxA;
    }
}