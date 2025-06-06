using SkiaSharp;
using System;
using System.IO;

namespace Barcoded
{
    public abstract class ImageWriter
    {
        // Abstract members to be implemented by derived classes like LinearBarcode
        protected abstract string BarcodeValue { get; }
        protected abstract SKBitmap CurrentImage { get; }
        protected abstract void PerformUpdateBarcode();
        protected abstract LinearEncoder EncoderInstance { get; }

        /// <summary>
        /// Saves the barcode image directly to a file with the correct DPI metadata,
        /// using the ImageCodec specified in the Encoder.
        /// </summary>
        /// <param name="destinationPath">The file path where the image will be saved.</param>
        public void SaveToFile(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(this.BarcodeValue))
            {
                throw new InvalidOperationException("No BarcodeValue set");
            }

            PerformUpdateBarcode();

            SKBitmap imageToSave = this.CurrentImage;
            if (imageToSave == null)
            {
                throw new InvalidOperationException("Failed to generate barcode image");
            }

            LinearEncoder encoder = this.EncoderInstance;
            // Convert Barcoded.ImageFormat to SKEncodedImageFormat for SkiaSharp
            SKEncodedImageFormat skFormat = ImageHelpers.ToSkiaImageFormat(encoder.ImageCodec);

            using (var skImage = SKImage.FromBitmap(imageToSave))
            using (var data = skImage.Encode(skFormat, 100))
            {
                byte[] rawBytes = data.ToArray();
                // Pass Barcoded.ImageFormat to InjectDpi
                byte[] withDpi = ImageDpiInjector.InjectDpi(rawBytes, encoder.Dpi, encoder.ImageCodec);
                File.WriteAllBytes(destinationPath, withDpi);
            }
        }

        /// <summary>
        /// Saves the barcode image directly to a file with the correct DPI metadata,
        /// using the specified image format.
        /// </summary>
        /// <param name="destinationPath">The file path where the image will be saved.</param>
        /// <param name="format">The Barcoded.ImageFormat to use for saving.</param>
        public void SaveImageToFile(string destinationPath, ImageFormat format) // Changed parameter type
        {
            if (string.IsNullOrWhiteSpace(this.BarcodeValue))
            {
                throw new InvalidOperationException("No BarcodeValue set");
            }

            PerformUpdateBarcode();

            SKBitmap imageToSave = this.CurrentImage;
            if (imageToSave == null)
            {
                throw new InvalidOperationException("Failed to generate barcode image");
            }

            LinearEncoder encoder = this.EncoderInstance;
            // Convert Barcoded.ImageFormat to SKEncodedImageFormat for SkiaSharp
            SKEncodedImageFormat skFormat = ImageHelpers.ToSkiaImageFormat(format);

            using (var skImage = SKImage.FromBitmap(imageToSave))
            using (var data = skImage.Encode(skFormat, 100))
            {
                byte[] rawBytes = data.ToArray();
                // Pass Barcoded.ImageFormat to InjectDpi
                byte[] withDpi = ImageDpiInjector.InjectDpi(rawBytes, encoder.Dpi, format);
                File.WriteAllBytes(destinationPath, withDpi);
            }
        }
    }
}
