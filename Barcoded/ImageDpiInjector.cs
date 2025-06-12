using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Barcoded
{
    public static class ImageDpiInjector
    {
        /// <summary>
        /// Injects DPI metadata into the image byte array.
        /// </summary>
        /// <param name="imageBytes">The raw image byte array.</param>
        /// <param name="dpi">The DPI value to inject.</param>
        /// <param name="format">The Barcoded.ImageFormat of the image.</param>
        /// <returns>Image byte array with DPI metadata injected.</returns>
        public static byte[] InjectDpi(byte[] imageBytes, int dpi, ImageFormat format)
        {
            // Convert Barcoded.ImageFormat to SKEncodedImageFormat for internal logic
            SKEncodedImageFormat skFormat = ImageHelpers.ToSkiaImageFormat(format);
            var result = imageBytes;
            switch (skFormat)
            {
                case SKEncodedImageFormat.Png:
                    result = InjectPhysChunk(imageBytes, dpi);
                    break;
                case SKEncodedImageFormat.Jpeg:
                    result = InjectJpegDpi(imageBytes, dpi);
                    break; // Supported formats
            }
            return result;
            //return skFormat switch
            //{
            //    SKEncodedImageFormat.Png => InjectPhysChunk(imageBytes, dpi),
            //    SKEncodedImageFormat.Jpeg => InjectJpegDpi(imageBytes, dpi),
            //    _ => imageBytes // BMP and WEBP: no change; either unsupported or don't store DPI
            //};
        }

        private static byte[] InjectJpegDpi(byte[] jpegBytes, int dpi)
        {
            using (MemoryStream input = new MemoryStream(jpegBytes))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    // Copy header (first two bytes: FF D8)
                    output.WriteByte((byte)input.ReadByte());
                    output.WriteByte((byte)input.ReadByte());

                    // Write APP0 segment (JFIF with DPI)
                    using (var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
                    {
                        bw.Write((byte)0xFF); // marker
                        bw.Write((byte)0xE0); // APP0
                        bw.Write(ToBigEndian((short)16)); // length
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("JFIF\0")); // identifier
                        bw.Write((byte)1); // major version
                        bw.Write((byte)1); // minor version
                        bw.Write((byte)1); // units: 1 = DPI
                        bw.Write(ToBigEndian((short)dpi)); // X density
                        bw.Write(ToBigEndian((short)dpi)); // Y density
                        bw.Write((byte)0); // X thumbnail
                        bw.Write((byte)0); // Y thumbnail
                    }

                    // Copy the rest
                    input.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        public static byte[] InjectPhysChunk(byte[] pngBytes, int dpi)
        {
            const double inchesPerMeter = 39.3701;
            int pixelsPerMeter = (int)(dpi * inchesPerMeter);

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    // PNG header
                    bw.Write(pngBytes, 0, 8);

                    // IHDR chunk (standard length: 13 bytes data + 4 bytes type + 4 bytes length + 4 bytes CRC = 25)
                    const int ihdrChunkLength = 13;
                    const int ihdrFullLength = ihdrChunkLength + 4 + 4 + 4;
                    bw.Write(pngBytes, 8, ihdrFullLength);

                    // Create pHYs chunk
                    using (MemoryStream chunkData = new MemoryStream())
                    {
                        using (BinaryWriter chunkWriter = new BinaryWriter(chunkData))
                        {
                            chunkWriter.Write(ToBigEndian(pixelsPerMeter)); // X axis
                            chunkWriter.Write(ToBigEndian(pixelsPerMeter)); // Y axis
                            chunkWriter.Write((byte)1); // unit = meter
                        }
                        byte[] physData = chunkData.ToArray();
                        byte[] chunkType = System.Text.Encoding.ASCII.GetBytes("pHYs");

                        bw.Write(ToBigEndian(physData.Length));
                        bw.Write(chunkType);
                        bw.Write(physData);
                        uint crc = Crc32(chunkType.Concat(physData).ToArray());
                        bw.Write(ToBigEndian(crc));
                    }
                    // Write rest of original PNG (after IHDR)
                    bw.Write(pngBytes, 8 + ihdrFullLength, pngBytes.Length - (8 + ihdrFullLength));
                    return ms.ToArray();
                }
            }
        }

        private static byte[] ToBigEndian(int value) => BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
        private static byte[] ToBigEndian(short value) => BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
        private static byte[] ToBigEndian(uint value) => BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)value));

        private static uint Crc32(byte[] data)
        {
            uint[] table = Crc32Table.Value;

            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
                crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];

            return ~crc;
        }

        private static readonly Lazy<uint[]> Crc32Table = new Lazy<uint[]>(() =>
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1;
                table[i] = c;
            }
            return table;
        });
    }
}
