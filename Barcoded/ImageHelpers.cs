using SkiaSharp;
using System;

namespace Barcoded
{
    internal static class ImageHelpers
    {
        /// <summary>
        /// Resizes the given font to fit within the specified width.
        /// </summary>
        /// <param name="stringLength">String to be measured.</param>
        /// <param name="width">Available width.</param>
        /// <param name="dpi">Image DPI.</param>
        /// <param name="font">Font to be resized.</param>
        /// <param name="limitSizeToFont">Limit maximum font size to the original font provided.</param>
        /// <returns>Font, size adjusted to fir width.</returns>
        internal static float GetSizedFontForWidth(int stringLength, int width, int dpi, SKTypeface font, float maxFontSize = 100)
        {
            return GetSizedFontForWidth(new string('\u0057', stringLength), width, dpi, font, maxFontSize);

        }

        /// <summary>
        /// Gets the font adjusted to the maximum size that will allow the given text to fit the given width.
        /// </summary>
        /// <param name="textToFit">Text that needs to fit</param>
        /// <param name="width">Available width</param>
        /// <param name="dpi">Image DPI</param>
        /// <param name="font">Font to be measured</param>
        /// <param name="maxFontSize">Limit maximum font size returned to the font size provided</param>
        /// <returns>Font set to the maximum size that will fit</returns>
        internal static float GetSizedFontForWidth(string textToFit, int width, int dpi, SKTypeface typeface, float maxFontSize = 100)
        {
            float fontSize = 1;
            float lastGoodSize = fontSize;
            for (; fontSize <= maxFontSize; fontSize++)
            {
                using (SKFont font = new SKFont(typeface, fontSize))
                {
                    float textWidth = font.MeasureText(textToFit);
                    if (textWidth < width)
                        lastGoodSize = fontSize;
                    else
                        break;
                }
            }
            return lastGoodSize;
        }

        /// <summary>
        /// Returns the image width required for the given text drawn using the specified font.
        /// </summary>
        /// <param name="text">String to be measured</param>
        /// <param name="font">Font to be used</param>
        /// <param name="dpi">Image DPI</param>
        /// <returns>Measured image size</returns>
        internal static SKSize GetStringElementSize(string text, SKTypeface typeface, float fontSize, int dpi)
        {
            using (SKFont font = new SKFont(typeface, fontSize))
            {
                float width = font.MeasureText(text);
                var metrics = font.Metrics;
                float height = font.Metrics.Descent - font.Metrics.Ascent;
                return new SKSize(width, height);
            }
        }

        /// <summary>
        /// Returns the image codec for the given codec name.
        /// </summary>
        /// <param name="codecName">Codec name.</param>
        /// <remarks>Will return PNG, if specified codec cannot be found.</remarks>
        /// <returns>Image codec.</returns>
        internal static ImageFormat FindCodecInfo(string codecName)
        {
            ImageFormat imageFormat = ImageFormat.Png; // Default to PNG if codec not found
            switch(codecName.ToUpper())
            {
                case "PNG":
                    imageFormat = ImageFormat.Png;
                    break;
                case "JPG":
                case "JPEG":
                    imageFormat = ImageFormat.Jpeg;
                    break;
                case "BMP":
                    imageFormat = ImageFormat.Bmp;
                    break;
                default:
                    // Unsupported codec, default to PNG
                    break;
            }
            return imageFormat;
        }

        /// <summary>
        /// Converts Barcoded.ImageFormat to SkiaSharp.SKEncodedImageFormat.
        /// </summary>
        /// <param name="format">The Barcoded.ImageFormat to convert.</param>
        /// <returns>The corresponding SkiaSharp.SKEncodedImageFormat.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the format is not supported.</exception>
        internal static SKEncodedImageFormat ToSkiaImageFormat(ImageFormat format)
        {
            SKEncodedImageFormat skFormat;
            switch (format)
            {
                case ImageFormat.Bmp:
                    skFormat = SKEncodedImageFormat.Bmp;
                    break;
                case ImageFormat.Jpeg:
                    skFormat = SKEncodedImageFormat.Jpeg;
                    break;
                case ImageFormat.Png:
                    skFormat = SKEncodedImageFormat.Png;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported image format: {format}");
            }
            return skFormat;
        }

    }
}
