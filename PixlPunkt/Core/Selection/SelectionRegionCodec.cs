using System;
using System.IO;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Run-length encoding of a <see cref="SelectionRegion"/> mask. Selection masks are large
    /// (one byte per canvas pixel) and almost entirely runs, so history snapshots of them are
    /// stored in this form instead of as raw copies.
    /// </summary>
    public static class SelectionRegionCodec
    {
        private const int Magic = 0x4C455253; // "SREL"

        public static byte[] Encode(SelectionRegion region)
        {
            var (w, h, ox, oy, mask) = region.ExportMask();
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(Magic);
            bw.Write(w); bw.Write(h); bw.Write(ox); bw.Write(oy);

            // Runs alternate starting with a run of zeros (possibly empty). Run length as Int32.
            int i = 0, n = mask.Length;
            byte current = 0;
            while (i < n)
            {
                int start = i;
                while (i < n && (mask[i] != 0) == (current != 0)) i++;
                bw.Write(i - start);
                current = (byte)(current == 0 ? 1 : 0);
            }
            return ms.ToArray();
        }

        public static void Decode(byte[] data, SelectionRegion into)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            if (br.ReadInt32() != Magic) throw new InvalidDataException("Not an encoded selection region.");
            int w = br.ReadInt32(), h = br.ReadInt32(), ox = br.ReadInt32(), oy = br.ReadInt32();
            var mask = new byte[Math.Max(0, w) * Math.Max(0, h)];

            int i = 0;
            byte current = 0;
            while (i < mask.Length && ms.Position < ms.Length)
            {
                int run = br.ReadInt32();
                if (run < 0 || i + run > mask.Length) throw new InvalidDataException("Corrupt selection region run.");
                if (current != 0) Array.Fill(mask, (byte)1, i, run);
                i += run;
                current = (byte)(current == 0 ? 1 : 0);
            }
            into.ImportMask(w, h, ox, oy, mask);
        }
    }
}
