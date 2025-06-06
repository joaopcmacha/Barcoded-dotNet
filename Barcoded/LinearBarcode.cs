using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Barcoded
{
    /// <summary>
    /// Linear (one-dimensional) barcode.
    /// </summary>
    public class LinearBarcode : ImageWriter
    {
        private bool _barcodeValueChanged;

        private string _internalBarcodeValue; // Renamed to avoid conflict with inherited abstract property
        /// <summary>
        /// Value to encode.
        /// </summary>
        public string ValueToEncode // Renamed for clarity, or use the abstract property directly
        {
            get => _internalBarcodeValue;
            set
            {
                _internalBarcodeValue = value;
                _barcodeValueChanged = true;
            }
        }

        protected SKBitmap _image;

        // Implementation for ImageWriter abstract members
        protected override string BarcodeValue => this.ValueToEncode; // Access the renamed property
        protected override SKBitmap CurrentImage => this._image;
        protected override void PerformUpdateBarcode() => this.UpdateBarcode();
        protected override LinearEncoder EncoderInstance => this.Encoder;

        /// <summary>
        /// Barcode symbology.
        /// </summary>
        public Symbology Symbology { get; }

        /// <summary>
        /// Barcode image.
        /// </summary>
        public SKBitmap Image
        {
            get
            {
                UpdateBarcode();
                return _image;
            }
        }

        private LinearVectors _vectors;
        /// <summary>
        /// Barcode vectors.
        /// </summary>
        public LinearVectors Vectors
        {
            get
            {
                UpdateBarcode();
                return _vectors;
            }
        }

        /// <summary>
        /// Minimum point width for the given encoding value.
        /// </summary>
        public int MinimumPointWidth
        {
            get
            {
                Encoder.Generate(ValueToEncode);
                return Encoder.LinearEncoding.MinimumWidth;
            }
        }

        public string ZplEncode
        {
            get
            {
                Encoder.Generate(ValueToEncode);
                return Encoder.ZplEncode;
            }
        }

        /// <summary>
        /// Barcode symbology encoder.
        /// </summary>
        public LinearEncoder Encoder { get; }

        /// <summary>
        /// Creates a barcode image from the declared value and desired symbology.
        /// </summary>
        /// <param name="barcodeValue">Barcode value string.</param>
        /// <param name="symbology">Barcode symbology string.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public LinearBarcode(string barcodeValue, string symbology) : this(barcodeValue, GetSymbology(symbology))
        {
        }

        /// <summary>
        /// Creates a barcode image from the declared value and desired symbology.
        /// </summary>
        /// <param name="barcodeValue">Barcode value string.</param>
        /// <param name="symbology">Barcode symbology</param>
        /// <exception cref="ArgumentNullException"></exception>
        public LinearBarcode(string barcodeValue, Symbology symbology)
        {
            this.ValueToEncode = barcodeValue ?? throw new ArgumentNullException(nameof(barcodeValue));
            Symbology = symbology;

            switch (symbology)
            {
                case Symbology.Code128ABC:
                    Encoder = new Code128Encoder(symbology);
                    break;
                case Symbology.Code128BAC:
                    Encoder = new Code128Encoder(symbology);
                    break;
                case Symbology.Code128AB:
                    Encoder = new Code128Encoder(symbology);
                    break;
                case Symbology.Code128BA:
                    Encoder = new Code128Encoder(symbology);
                    break;
                case Symbology.GS1128:
                    Encoder = new Code128Encoder(symbology);
                    break;
                case Symbology.Code39:
                    Encoder = new Code39Encoder(symbology);
                    break;
                case Symbology.Code39C:
                    Encoder = new Code39Encoder(symbology);
                    break;
                case Symbology.Code39Full:
                    Encoder = new Code39Encoder(symbology);
                    break;
                case Symbology.Code39FullC:
                    Encoder = new Code39Encoder(symbology);
                    break;
                case Symbology.I2of5:
                    Encoder = new Interleaved2Of5Encoder(symbology);
                    break;
                case Symbology.I2of5C:
                    Encoder = new Interleaved2Of5Encoder(symbology);
                    break;
                case Symbology.Ean13:
                    Encoder = new Ean138Encoder(symbology);
                    break;
                case Symbology.UpcA:
                    Encoder = new Ean138Encoder(symbology);
                    break;
                case Symbology.Ean8:
                    Encoder = new Ean138Encoder(symbology);
                    break;
                default:
                    Encoder = new Code128Encoder(Symbology.Code128BAC);
                    break;
            }
        }

        /// <summary>
        /// Get a list of available barcode symbologies.
        /// </summary>
        /// <returns>Returns barcode symbology text list.</returns>
        public static List<string> GetSymbologies()
        {
            List<string> symbologies = Enum.GetValues(typeof(Symbology))
                .Cast<Symbology>()
                .Select(v => v.ToString())
                .ToList();

            return symbologies;
        }

        /// <summary>
        /// Get a list of available human readable text positions.
        /// </summary>
        /// <returns>Returns human readable text list.</returns>
        public static List<string> GetHumanReadablePositions()
        {
            List<string> humanReadablePositions = Enum.GetValues(typeof(HumanReadablePosition))
                .Cast<HumanReadablePosition>()
                .Select(v => v.ToString())
                .ToList();

            return humanReadablePositions;
        }

        /// <summary>
        /// Returns the symbology from the given name.
        /// </summary>
        /// <param name="symbology">Symbology name.</param>
        /// <returns>Symbology.</returns>
        private static Symbology GetSymbology(string symbology)
        {
            symbology = symbology ?? "";

            switch (symbology.ToUpper())
            {
                case "CODE128ABC":
                    return Symbology.Code128ABC;
                case "CODE128BAC":
                    return Symbology.Code128BAC;
                case "CODE128AB":
                    return Symbology.Code128AB;
                case "CODE128BA":
                    return Symbology.Code128BA;
                case "GS1128":
                    return Symbology.GS1128;
                case "CODE39":
                    return Symbology.Code39;
                case "CODE39C":
                    return Symbology.Code39C;
                case "CODE39FULL":
                    return Symbology.Code39Full;
                case "CODE39FULLC":
                    return Symbology.Code39FullC;
                case "I2OF5":
                    return Symbology.I2of5;
                case "I2OF5C":
                    return Symbology.I2of5C;
                case "EAN13":
                    return Symbology.Ean13;
                case "UPCA":
                    return Symbology.UpcA;
                case "EAN8":
                    return Symbology.Ean8;
                default:
                    return Symbology.Code128BAC;
            }
        }

        /// <summary>
        /// Provides a byte array version of the barcode image in the declared format for saving to file.
        /// </summary>
        /// <param name="codec">Codec for the image format to be saved.</param>
        /// <returns>Byte array of the barcode image.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public byte[] SaveImage(string codec)
        {
            Encoder.CodecName = codec ?? throw new ArgumentNullException(nameof(codec));

            if (string.IsNullOrWhiteSpace(ValueToEncode))
            {
                throw new InvalidOperationException("No BarcodeValue set");
            }

            using var imageMemoryStream = Encoder.GetImage(ValueToEncode);
            imageMemoryStream.Position = 0;
            using var skiaImage = SKImage.FromEncodedData(imageMemoryStream);
            _image = skiaImage?.ToBitmap() ?? throw new InvalidOperationException("Failed to create image from stream.");
            _vectors = new LinearVectors(Encoder);
            _barcodeValueChanged = false;
            return imageMemoryStream.ToArray();
        }

        public static SKBitmap ToBitmap(SKImage image)
        {
            return image != null ? SKBitmap.FromImage(image) : null;
        }

        /// <summary>
        /// Checks if any barcode settings have changed since the last call and creates a new barcode if they have.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        protected virtual void UpdateBarcode()
        {
            if (string.IsNullOrWhiteSpace(this.ValueToEncode))
            {
                throw new InvalidOperationException("No BarcodeValue set");
            }

            if (_barcodeValueChanged | Encoder.PropertyChanged)
            {
                using var imageMemoryStream = Encoder.GetImage(this.ValueToEncode);
                imageMemoryStream.Position = 0;
                using var skiaImage = SKImage.FromEncodedData(imageMemoryStream);
                _image = skiaImage?.ToBitmap();
                _vectors = new LinearVectors(Encoder);
                _barcodeValueChanged = false;
                Encoder.ResetPropertyChanged();
            }
        }
    }

    public static class SkiaExtensions
    {
        public static SKBitmap ToBitmap(this SKImage image)
        {
            if (image == null) return null;
            using var data = image.Encode();
            return SKBitmap.Decode(data);
        }
    }
}
