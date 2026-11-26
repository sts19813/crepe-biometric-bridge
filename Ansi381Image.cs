using SourceAFIS;

namespace CrepeBiometricBridge;

internal static class Ansi381Image
{
    private const int StandardDataSizeOffset = 8;
    private const int StandardDataOffsetOffset = 12;
    private const int HeaderSize = 38;
    private const int BlockHeaderSize = 14;

    public static FingerprintImage Read(byte[] bir)
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

        return new FingerprintImage(width, height, bir[pixelsOffset..(pixelsOffset + pixelCount)], new FingerprintImageOptions { Dpi = dpi });
    }
}
