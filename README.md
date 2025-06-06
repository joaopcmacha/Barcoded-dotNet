![Barcoded Icon](SVGs/Barcode_Style1.svg)
# Barcoded
A C#/.NET library to generate barcode images.

Upgraded to .Net 8 and SkiaSharp.

(Based on the [original code]() from Brett Reynolds)


## Usage
```C#
LinearBarcode newBarcode = new LinearBarcode("SomeValue", Symbology.Code128BAC)
    {
    Encoder =
        {
            Dpi = 300,
            BarcodeHeight = 200
        }
    };
```

## Features

* **Supported Symbologies**
  - Code128 (Subsets A,B & C)
  - Code39 (Standard & Full ASCII)
  - GS1-128
  - EAN-13
  - EAN-8
  - UPC-A
  - Interleaved 2 of 5
  
* **Human Readable Label**
  - Discrete Text
  ```C#
  newBarcode.Encoder.HumanReadableValue = "S O M E V A L U E";
  ```
  - Placement
  ```C#
  newBarcode.Encoder.SetHumanReadablePosition("Above");
  ```
  - Font
  ```C#
  newBarcode.Encoder.SetHumanReadableFont("Arial", 8);
  ```
* **Match X Dimension (narrow bar) to desired width**
  ```C#
  newBarcode.TargetWidth = 400;
  ```

* **Show encoding characters**
  ```C#
  newBarcode.Encoder.ShowEncoding = true;
  ```

* **Output to ZPL string**
  ```C#
  string zplString = newBarcode.ZplEncode;
  ```

* **Include quietzone**
  ```C#
  newBarcode.Encoder.Quietzone = true;
  ```
  
* **Save to file**
  ```C#
  newBarcode.SaveImageToFile(destinationPath, Barcoded.ImageFormat.Png);
  ```