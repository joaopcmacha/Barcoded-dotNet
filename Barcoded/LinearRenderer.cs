using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;

namespace Barcoded
{
    internal static class LinearRenderer
    {
        private const int MaximumPixelWidth = 12000; // 20" at maximum DPI of 600

        /// <summary>
        /// Holds the x & y position of the ImageElement.
        /// </summary>
        internal class Position
        {
            internal int XPosition { get; set; }
            internal int YPosition { get; set; }

            internal Position(int xPosition = 0, int yPosition = 0)
            {
                XPosition = xPosition;
                YPosition = yPosition;
            }
        }

        /// <summary>
        /// Holds the width and height of the ImageElement.
        /// </summary>
        internal class Size
        {
            internal int Width { get; set; }
            internal int Height { get; set; }

            internal Size(int width = 0, int height = 0)
            {
                Width = width;
                Height = height;
            }
        }

        /// <summary>
        /// Holds the element image, dimensions and position.
        /// </summary>
        internal class ImageElement
        {
            internal SKBitmap Image { get; set; }
            internal Position Position { get; set; }
            internal Size Size { get; set; }

            internal ImageElement()
            {
                Image = new SKBitmap(1, 1);
                Position = new Position();
                Size = new Size();
            }

            internal ImageElement(SKBitmap image)
            {
                Image = image;
                Position = new Position();
                Size = new Size(image.Width, image.Height);
            }

            internal void UpdateImage(SKBitmap image)
            {
                Image = image;
                Size.Width = image.Width;
                Size.Height = image.Height;
            }
        }

        private static void Draw(ref MemoryStream memoryStream, LinearEncoder linearEncoder)
        {
            int xDimensionOriginal = linearEncoder.XDimension;

            // Adjust x-dimension to match target width (if set)
            linearEncoder.XDimension = Math.Max(linearEncoder.XDimension, GetXDimensionForTargetWidth(linearEncoder.TargetWidth, linearEncoder.LinearEncoding.MinimumWidth));

            // Ensure the x-dimension selected doesn't result in a width that exceeds maximum allowable.
            linearEncoder.XDimension = Math.Min(linearEncoder.XDimension, GetXDimensionForTargetWidth(MaximumPixelWidth, linearEncoder.LinearEncoding.MinimumWidth));

            // Set any necessary behavior dependent on symbology.
            switch (linearEncoder.Symbology)
            {
                case Symbology.Ean13:
                case Symbology.UpcA:
                case Symbology.Ean8:
                    linearEncoder.Quietzone = true;
                    linearEncoder.HumanReadableSymbolAligned = true;
                    break;
            }

            int quietzone = 0;
            if (linearEncoder.Quietzone)
            {
                quietzone = Math.Max(20 * linearEncoder.XDimension, linearEncoder.Dpi / 4) / 2;
            }

            // Create each of the image elements.
            ImageElement encodingTextImage = GetEncodingImage(linearEncoder, quietzone);
            ImageElement barcodeImage = GetBarcodeImage(linearEncoder, quietzone);

            linearEncoder.BarcodeWidth = barcodeImage.Size.Width;

            ImageElement humanReadableImage = linearEncoder.HumanReadableSymbolAligned ? GetHumanReadableImageSymbolAligned(linearEncoder, quietzone) : GetHumanReadableImageCentered(linearEncoder, quietzone);

            // Adjust each element position, dependent on label text visibility and position.
            switch (linearEncoder.HumanReadablePosition)
            {
                case HumanReadablePosition.Above:     //Label above the barcode
                    barcodeImage.Position.YPosition += humanReadableImage.Size.Height;
                    encodingTextImage.Position.YPosition += humanReadableImage.Size.Height + barcodeImage.Size.Height;
                    break;
                case HumanReadablePosition.Embedded:     //Embedded the barcode
                    barcodeImage.Position.YPosition += encodingTextImage.Size.Height;
                    humanReadableImage.Position.YPosition += encodingTextImage.Size.Height + barcodeImage.Size.Height - (humanReadableImage.Size.Height / 2);
                    break;
                default:    //Label below the barcode
                    barcodeImage.Position.YPosition += encodingTextImage.Size.Height;
                    humanReadableImage.Position.YPosition += encodingTextImage.Size.Height + barcodeImage.Size.Height;
                    break;
            }

            // Set the required image height by adding the barcode height to the encoding text or value label text heights (if visible)
            int imageHeight = barcodeImage.Size.Height + encodingTextImage.Size.Height + humanReadableImage.Size.Height;

            // Reduce the image height if human readable position is embedded
            if (linearEncoder.HumanReadablePosition == HumanReadablePosition.Embedded)
            {
                imageHeight -= (humanReadableImage.Size.Height / 2);
            }

            // Set the required image width by taking the greater of the barcode width and value label text width (if used).
            int imageWidth = Math.Max(barcodeImage.Size.Width + (quietzone * 2), humanReadableImage.Size.Width);

            // Create the combined image.
            SKBitmap combinedImage = new SKBitmap(imageWidth, imageHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (SKCanvas combinedGraphics = new SKCanvas(combinedImage))
            {


                // Add each element to the combined image.
                using (var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
                {
                    combinedGraphics.DrawRect(0, 0, combinedImage.Width, combinedImage.Height, paint);
                }
                combinedGraphics.DrawBitmap(barcodeImage.Image, barcodeImage.Position.XPosition, barcodeImage.Position.YPosition);
                combinedGraphics.DrawBitmap(encodingTextImage.Image, encodingTextImage.Position.XPosition, encodingTextImage.Position.YPosition);
                combinedGraphics.DrawBitmap(humanReadableImage.Image, humanReadableImage.Position.XPosition, humanReadableImage.Position.YPosition);

                // Save the image to the memory stream.
                // Convert Barcoded.ImageFormat to SKEncodedImageFormat for SkiaSharp
                SKEncodedImageFormat skFormat = ImageHelpers.ToSkiaImageFormat(linearEncoder.ImageCodec);
                using (var image = SKImage.FromBitmap(combinedImage))
                using (var data = image.Encode(skFormat, 100))
                {
                    var rawBytes = data.ToArray();
                    var withDpi = ImageDpiInjector.InjectDpi(rawBytes, linearEncoder.Dpi, linearEncoder.ImageCodec);
                    memoryStream.Write(withDpi, 0, withDpi.Length);
                    memoryStream.Position = 0;
                }

                // Set flag if xdimension was changed.
                if (linearEncoder.XDimension != xDimensionOriginal)
                {
                    linearEncoder.XDimensionChanged = true;
                }

                // Dispose of the objects we won't need any more.
                barcodeImage.Image.Dispose();
                humanReadableImage.Image.Dispose();
                encodingTextImage.Image.Dispose();
                combinedGraphics.Dispose();
                combinedImage.Dispose();
            }
            linearEncoder.ResetPropertyChanged();
        }

        internal static MemoryStream DrawImageMemoryStream(LinearEncoder linearEncoder)
        {
            MemoryStream memoryStream = new MemoryStream();
            Draw(ref memoryStream, linearEncoder);
            return memoryStream;
        }

        /// <summary>
        /// Creates the label value image element.
        /// </summary>
        /// <param name="linearEncoder"></param>
        /// <param name="quietzone"></param>
        /// <returns>The generated label value image inside an ImageElement object.</returns>
        internal static ImageElement GetHumanReadableImageCentered(LinearEncoder linearEncoder, int quietzone)
        {
            // Create an empty ImageElement
            ImageElement humanReadableElement = new ImageElement();

            // If the human readable position is set to hidden, return the empty ImageElement
            if (linearEncoder.HumanReadablePosition == HumanReadablePosition.Hidden)
            {
                return humanReadableElement;
            }

            // If the human readable is set to a visible position, but has no value, take the barcode value
            if (string.IsNullOrWhiteSpace(linearEncoder.HumanReadableValue))
            {
                linearEncoder.HumanReadableValue = linearEncoder.EncodedValue;
            }

            // Calculate the barcode image width from the minimum width multiplied by the x-dimension
            int barcodeWidth = linearEncoder.LinearEncoding.MinimumWidth * linearEncoder.XDimension;

            // Get the original human readable font size, so we can compare with the size after adjusting to fir barcode width
            int humanReadableFontSizeOriginal = (int)linearEncoder.HumanReadableFontSize;

            // Adjust the human readable font size so that the value does not exceed the width of the barcode image
            linearEncoder.HumanReadableFontSize = ImageHelpers.GetSizedFontForWidth(linearEncoder.HumanReadableValue, barcodeWidth, linearEncoder.Dpi, linearEncoder.HumanReadableFont);

            // Set the human readable font size changed flag, if size different from original
            if (humanReadableFontSizeOriginal != (int)linearEncoder.HumanReadableFontSize)
            {
                linearEncoder.HumanReadableFontSizeChanged = true;
            }

            // Measure the value label text size, based on the font provided
            SKSize labelTextSize = ImageHelpers.GetStringElementSize(linearEncoder.HumanReadableValue, linearEncoder.HumanReadableFont, linearEncoder.HumanReadableFontSize, linearEncoder.Dpi);

            // Create a new bitmap image for the label value text based on the calculated dimensions
            humanReadableElement.UpdateImage(
                new SKBitmap((int)Math.Ceiling(labelTextSize.Width), (int)Math.Ceiling(labelTextSize.Height), SKColorType.Bgra8888, SKAlphaType.Premul)
            );

            // Create a new graphics to draw on the barcode image
            using (SKCanvas labelValueGraphics = new SKCanvas(humanReadableElement.Image))
            {

                using (var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
                {
                    labelValueGraphics.DrawRect(1, 1, humanReadableElement.Image.Width, humanReadableElement.Image.Height, paint);
                }

                using (SKFont font = new SKFont(linearEncoder.HumanReadableFont, linearEncoder.HumanReadableFontSize))
                {
                    using (SKPaint paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                    {
                        labelValueGraphics.DrawText(linearEncoder.HumanReadableValue, 1, 1 + font.Size, font, paint);
                    }
                }

                labelValueGraphics.Flush();
            }
            humanReadableElement.Position.XPosition = quietzone + (barcodeWidth - (int)labelTextSize.Width) / 2;

            return humanReadableElement;
        }

        /// <summary>
        /// Creates the label value image element.
        /// </summary>
        /// <param name="linearEncoder"></param>
        /// <param name="quietzone"></param>
        /// <returns>The generated label value image inside an ImageElement object.</returns>
        internal static ImageElement GetHumanReadableImageSymbolAligned(LinearEncoder linearEncoder, int quietzone)
        {
            // Create an empty ImageElement
            ImageElement humanReadableElement = new ImageElement();

            // If the human readable position is set to hidden, return the empty ImageElement
            if (linearEncoder.HumanReadablePosition == HumanReadablePosition.Hidden)
            {
                return humanReadableElement;
            }

            // Setup the label font with initial value "A" to assist calculating label element height later
            float humanReadableFontSize = ImageHelpers.GetSizedFontForWidth(
                1,
                linearEncoder.LinearEncoding.GetWidestSymbol() * linearEncoder.XDimension,
                linearEncoder.Dpi,
                linearEncoder.HumanReadableFont
            );

            // Set the text size so that we can get the encoding element height later
            SKSize humanReadableSize = ImageHelpers.GetStringElementSize(
                "W",
                linearEncoder.HumanReadableFont,
                humanReadableFontSize,
                linearEncoder.Dpi
            );

            int prefixWidth = linearEncoder.LinearEncoding.HumanReadablePrefix?.Length * (int)humanReadableSize.Width ?? 0;
            int suffixWidth = linearEncoder.LinearEncoding.HumanReadableSuffix?.Length * (int)humanReadableSize.Width ?? 0;

            // Set the encoding image width from the minimum width multiplied by the x-dimension
            int humanReadableImageWidth = linearEncoder.LinearEncoding.MinimumWidth * linearEncoder.XDimension + prefixWidth + suffixWidth;

            //Create a new bitmap image for the encoding text based on the calculated dimensions
            humanReadableElement.UpdateImage(
                new SKBitmap(humanReadableImageWidth, (int)Math.Ceiling(humanReadableSize.Height), SKColorType.Bgra8888, SKAlphaType.Premul)
            );

            using (SKCanvas humanReadableGraphics = new SKCanvas(humanReadableElement.Image))
            {

                // Preencher fundo de branco
                using (var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
                {
                    humanReadableGraphics.DrawRect(0, 0, humanReadableElement.Image.Width, humanReadableElement.Image.Height, bgPaint);
                }

                int xPosition = 0;
                int yPosition = 0;

                using (var font = new SKFont(linearEncoder.HumanReadableFont, humanReadableFontSize))
                {
                    using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                    {

                        // Add any human readable prefix
                        if (!string.IsNullOrEmpty(linearEncoder.LinearEncoding.HumanReadablePrefix))
                        {
                            string prefix = linearEncoder.LinearEncoding.HumanReadablePrefix;
                            float prefixTextWidth = font.MeasureText(prefix, paint);
                            float prefixX = xPosition + (prefixWidth - prefixTextWidth) / 2f;
                            float prefixY = yPosition + font.Size;
                            humanReadableGraphics.DrawText(prefix, prefixX, prefixY, SKTextAlign.Center, font, paint);
                            xPosition += prefixWidth;
                        }

                        // Draw each symbol
                        for (int symbol = 0; symbol <= linearEncoder.LinearEncoding.Symbols.Count - 1; symbol++)
                        {
                            string humanReadableCharacter = linearEncoder.LinearEncoding.Symbols[symbol].Character;
                            int symbolWidth = linearEncoder.LinearEncoding.Symbols[symbol].Width;

                            if (linearEncoder.LinearEncoding.Symbols[symbol].CharacterType == 0)
                            {
                                float charTextWidth = font.MeasureText(humanReadableCharacter, paint);
                                float charX = xPosition + (symbolWidth * linearEncoder.XDimension - charTextWidth) / 2f;
                                float charY = yPosition + font.Size;
                                humanReadableGraphics.DrawText(humanReadableCharacter, charX, charY, SKTextAlign.Center, font, paint);
                            }

                            xPosition += symbolWidth * linearEncoder.XDimension;
                        }

                        // Add any human readable suffix
                        if (!string.IsNullOrEmpty(linearEncoder.LinearEncoding.HumanReadableSuffix))
                        {
                            string suffix = linearEncoder.LinearEncoding.HumanReadableSuffix;
                            float suffixTextWidth = font.MeasureText(suffix, paint);
                            float suffixX = xPosition + (suffixWidth - suffixTextWidth) / 2f;
                            float suffixY = yPosition + font.Size;
                            humanReadableGraphics.DrawText(suffix, suffixX, suffixY, SKTextAlign.Center, font, paint);
                        }
                    }
                }
            }

            humanReadableElement.Position.XPosition = quietzone - prefixWidth;

            return humanReadableElement;
        }

        /// <summary>
        /// Creates the barcode image element.
        /// </summary>
        /// <param name="linearEncoder"></param>
        /// <param name="quietzone"></param>
        /// <returns>The generated barcode image inside an ImageElement object.</returns>
        internal static ImageElement GetBarcodeImage(LinearEncoder linearEncoder, int quietzone)
        {
            // Set the encoding image width from the minimum width multiplied by the x-dimension
            int barcodeImageWidth = linearEncoder.LinearEncoding.MinimumWidth * linearEncoder.XDimension;

            // Cria o bitmap SkiaSharp para o código de barras
            ImageElement barcodeElement = new ImageElement(
                new SKBitmap(barcodeImageWidth, linearEncoder.BarcodeHeight, SKColorType.Bgra8888, SKAlphaType.Premul)
            );

            using (var barcodeGraphics = new SKCanvas(barcodeElement.Image))
            {

                int xPosition = 0;
                int yPosition = 0;

                // Preenche o fundo de branco
                using (var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
                {
                    barcodeGraphics.DrawRect(xPosition, yPosition, barcodeElement.Image.Width, barcodeElement.Image.Height, paint);
                }

                // Percorre cada símbolo codificado e desenha barras e espaços
                for (int symbol = 0; symbol <= linearEncoder.LinearEncoding.Symbols.Count - 1; symbol++)
                {
                    LinearPattern symbolPattern = linearEncoder.LinearEncoding.Symbols[symbol].Pattern;

                    for (int module = 0; module <= symbolPattern.Count - 1; module++)
                    {
                        switch (symbolPattern[module].ModuleType)
                        {
                            case ModuleType.Bar: // Bar
                                int barWidth = symbolPattern[module].Width * linearEncoder.XDimension;
                                using (var barPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
                                {
                                    barcodeGraphics.DrawRect(xPosition, yPosition, barWidth, linearEncoder.BarcodeHeight, barPaint);
                                }
                                xPosition += barWidth;
                                break;
                            case ModuleType.Space: // Space
                                int spaceWidth = symbolPattern[module].Width * linearEncoder.XDimension;
                                xPosition += spaceWidth;
                                break;
                        }
                    }
                }
            }

            barcodeElement.Position.XPosition = quietzone;
            return barcodeElement;
        }

        /// <summary>
        /// Creates the encoding text image element.
        /// </summary>
        /// <param name="linearEncoder"></param>
        /// <param name="quietzone"></param>
        /// <returns>The generated encoding text image inside an ImageElement object.</returns>
        internal static ImageElement GetEncodingImage(LinearEncoder linearEncoder, int quietzone)
        {
            // Create an empty ImageElement
            ImageElement encodingTextElement = new ImageElement();

            // Return the empty ImageElement if encoding not visible
            if (linearEncoder.ShowEncoding == false)
            {
                return encodingTextElement;
            }

            // Setup the font for encoding com valor inicial "A" para calcular altura
            float encodingFontSize = ImageHelpers.GetSizedFontForWidth(
                1,
                linearEncoder.LinearEncoding.GetWidestSymbol() * linearEncoder.XDimension,
                linearEncoder.Dpi,
                linearEncoder.EncodingFontFamily
            );

            // Medir altura do texto
            SKSize encodingTextSize = ImageHelpers.GetStringElementSize(
                "A",
                linearEncoder.EncodingFontFamily,
                encodingFontSize,
                linearEncoder.Dpi
            );

            // Largura da imagem de encoding
            int encodingImageWidth = linearEncoder.LinearEncoding.MinimumWidth * linearEncoder.XDimension;

            // Cria o bitmap SkiaSharp para o encoding
            encodingTextElement.UpdateImage(
                new SKBitmap(encodingImageWidth, (int)Math.Ceiling(encodingTextSize.Height), SKColorType.Bgra8888, SKAlphaType.Premul)
            );

            using (var encodingTextGraphics = new SKCanvas(encodingTextElement.Image))
            {

                // Preencher fundo de branco
                using (var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
                {
                    encodingTextGraphics.DrawRect(0, 0, encodingImageWidth, (int)Math.Ceiling(encodingTextSize.Height), bgPaint);
                }

                int xPosition = 0;
                int yPosition = 0;

                using (var font = new SKFont(linearEncoder.EncodingFontFamily, encodingFontSize))
                {
                    using (var paint = new SKPaint { IsAntialias = true })
                    {

                        for (int symbol = 0; symbol <= linearEncoder.LinearEncoding.Symbols.Count - 1; symbol++)
                        {
                            string encodeCharacter = linearEncoder.LinearEncoding.Symbols[symbol].Character;
                            int symbolWidth = linearEncoder.LinearEncoding.Symbols[symbol].Width;

                            // Ajusta fonte para o símbolo atual, se necessário
                            float symbolFontSize = ImageHelpers.GetSizedFontForWidth(
                                encodeCharacter.Length,
                                symbolWidth * linearEncoder.XDimension,
                                linearEncoder.Dpi,
                                linearEncoder.EncodingFontFamily
                            );
                            font.Size = symbolFontSize;

                            // Determina cor de fundo e texto
                            if (linearEncoder.LinearEncoding.Symbols[symbol].CharacterType == 1)
                            {
                                // Fundo preto, texto branco
                                using (var barPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
                                {
                                    encodingTextGraphics.DrawRect(xPosition, yPosition, symbolWidth * linearEncoder.XDimension, encodingTextElement.Image.Height, barPaint);
                                }
                                paint.Color = SKColors.White;
                            }
                            else
                            {
                                // Fundo branco, texto preto, desenha retângulo de borda
                                using (var borderPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                                {
                                    encodingTextGraphics.DrawRect(
                                        xPosition,
                                        yPosition,
                                        (symbolWidth * linearEncoder.XDimension) - 1,
                                        encodingTextElement.Image.Height - 1,
                                        borderPaint
                                    );
                                }
                                paint.Color = SKColors.Black;
                            }

                            // Centraliza o texto no bloco
                            float charTextWidth = font.MeasureText(encodeCharacter);
                            float charX = xPosition + ((symbolWidth * linearEncoder.XDimension) - charTextWidth) / 2f;
                            float charY = yPosition + font.Size;

                            encodingTextGraphics.DrawText(encodeCharacter, charX, charY, SKTextAlign.Center, font, paint);

                            xPosition += symbolWidth * linearEncoder.XDimension;
                        }
                    }
                }
            }

            encodingTextElement.Position.XPosition = quietzone;

            return encodingTextElement;
        }

        /// <summary>
        /// Calculates the maximum x-dimension of a barcode for a given width.
        /// </summary>
        /// <param name="targetWidth">The target pixel width.</param>
        /// <param name="minimumWidth">The minimum barcode pixel width.</param>
        /// <returns>Maximum achievable x-dimension.</returns>
        internal static int GetXDimensionForTargetWidth(int targetWidth, int minimumWidth)
        {
            int xDimension = 1;
            if (targetWidth > 0)
            {
                while (!(minimumWidth * (xDimension + 1) > targetWidth))
                {
                    xDimension += 1;
                }
            }
            return xDimension;
        }

        /// <summary>
        /// Creates the final barcode image and writes it directly to the specified file path.
        /// This method handles DPI injection and file writing so that the final user does not need to use SkiaSharp directly.
        /// </summary>
        /// <param name="linearEncoder">The encoder containing the barcode details.</param>
        /// <param name="destinationPath">The destination file path where the image will be saved.</param>
        public static void WriteImageToFile(LinearEncoder linearEncoder, string destinationPath)
        {
            using (MemoryStream ms = DrawImageMemoryStream(linearEncoder))
            {
                System.IO.File.WriteAllBytes(destinationPath, ms.ToArray());
            }
        }

    }
}
