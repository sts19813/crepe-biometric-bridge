using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SourceAFIS;

namespace CrepeBiometricBridge;

internal sealed record CapturedFingerprint(FingerprintTemplate Template, Bitmap Preview);

internal static class Ansi381Image
{
    private const int StandardDataSizeOffset = 8;
    private const int StandardDataOffsetOffset = 12;
    private const int HeaderSize = 40;
    private const int BlockHeaderSize = 16;

    public static CapturedFingerprint Read(byte[] bir)
    {
        if (bir.Length < 16)
        {
            throw new InvalidOperationException("La muestra del lector está incompleta.");
        }

        var standardDataSize = BitConverter.ToInt32(bir, StandardDataSizeOffset);
        var standardDataOffset = BitConverter.ToInt32(bir, StandardDataOffsetOffset);

        if (standardDataOffset < 0 || standardDataSize < HeaderSize + BlockHeaderSize || standardDataOffset > bir.Length - standardDataSize)
        {
            throw new InvalidOperationException("El lector devolvió un formato de huella no compatible.");
        }

        if (bir[standardDataOffset + 34] != 8 || bir[standardDataOffset + 35] != 0)
        {
            throw new InvalidOperationException("La muestra requiere compresión o una profundidad de color no compatible.");
        }

        var width = BitConverter.ToUInt16(bir, standardDataOffset + HeaderSize + 4);
        var height = BitConverter.ToUInt16(bir, standardDataOffset + HeaderSize + 6);
        var pixelCount = checked(width * height);
        var pixelsOffset = standardDataOffset + HeaderSize + BlockHeaderSize;
        var standardDataEnd = standardDataOffset + standardDataSize;

        if (width == 0 || height == 0 || pixelsOffset > standardDataEnd - pixelCount)
        {
            throw new InvalidOperationException("La muestra no contiene una imagen de huella utilizable.");
        }

        var dpi = BitConverter.ToUInt16(bir, standardDataOffset + 28);

        if (bir[standardDataOffset + 33] == 2)
        {
            dpi = checked((ushort)Math.Round(dpi * 2.54));
        }

        if (dpi == 0)
        {
            dpi = 500;
        }

        var pixels = bir[pixelsOffset..(pixelsOffset + pixelCount)];
        var template = new FingerprintTemplate(new FingerprintImage(width, height, pixels, new FingerprintImageOptions { Dpi = dpi }));

        return new CapturedFingerprint(template, CreatePreview(width, height, pixels));
    }

    private static Bitmap CreatePreview(int width, int height, byte[] pixels)
    {
        var preview = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var data = preview.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        try
        {
            var stride = Math.Abs(data.Stride);
            var rgb = new byte[checked(stride * height)];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = pixels[(y * width) + x];
                    var offset = (y * stride) + (x * 3);
                    rgb[offset] = value;
                    rgb[offset + 1] = value;
                    rgb[offset + 2] = value;
                }
            }

            Marshal.Copy(rgb, 0, data.Scan0, rgb.Length);
        }
        finally
        {
            preview.UnlockBits(data);
        }

        return preview;
    }
}
