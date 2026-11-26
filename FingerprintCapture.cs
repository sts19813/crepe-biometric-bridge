using System.Runtime.InteropServices;
using SourceAFIS;

namespace CrepeBiometricBridge;

internal static class FingerprintCapture
{
    private static readonly SemaphoreSlim CaptureGate = new(1, 1);
    private const uint FingerprintFactor = 8;
    private const uint SystemPool = 1;
    private const uint RawSession = 1;
    private const uint VerifyPurpose = 2;
    private const uint RawSample = 0x20;

    public static async Task<CapturedFingerprint> CaptureAsync(CancellationToken cancellationToken)
    {
        await CaptureGate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => Capture(cancellationToken), cancellationToken);
        }
        finally
        {
            CaptureGate.Release();
        }
    }

    private static CapturedFingerprint Capture(CancellationToken cancellationToken)
    {
        var session = UIntPtr.Zero;
        var sample = IntPtr.Zero;

        try
        {
            var openResult = WinBioOpenSession(FingerprintFactor, SystemPool, RawSession, IntPtr.Zero, UIntPtr.Zero, IntPtr.Zero, out session);

            if (openResult != 0)
            {
                throw new InvalidOperationException($"No se pudo abrir el lector. Ejecuta el checador como administrador. Código: {openResult}.");
            }

            using var cancellationRegistration = cancellationToken.Register(() => WinBioCancel(session));
            var captureResult = WinBioCaptureSample(session, VerifyPurpose, RawSample, out _, out sample, out var sampleSize, out var rejectDetail);

            cancellationToken.ThrowIfCancellationRequested();

            if (captureResult != 0)
            {
                throw new InvalidOperationException($"No se pudo capturar la huella. Código: {captureResult}; detalle: {rejectDetail}.");
            }

            var bir = new byte[checked((int)sampleSize.ToUInt64())];
            Marshal.Copy(sample, bir, 0, bir.Length);

            return Ansi381Image.Read(bir);
        }
        finally
        {
            if (sample != IntPtr.Zero)
            {
                WinBioFree(sample);
            }

            if (session != UIntPtr.Zero)
            {
                WinBioCloseSession(session);
            }
        }
    }

    [DllImport("winbio.dll")]
    private static extern int WinBioOpenSession(uint factor, uint pool, uint flags, IntPtr unitArray, UIntPtr unitCount, IntPtr databaseId, out UIntPtr sessionHandle);

    [DllImport("winbio.dll")]
    private static extern int WinBioCaptureSample(UIntPtr sessionHandle, uint purpose, uint flags, out uint unitId, out IntPtr sample, out UIntPtr sampleSize, out uint rejectDetail);

    [DllImport("winbio.dll")]
    private static extern int WinBioCancel(UIntPtr sessionHandle);

    [DllImport("winbio.dll")]
    private static extern int WinBioFree(IntPtr address);

    [DllImport("winbio.dll")]
    private static extern int WinBioCloseSession(UIntPtr sessionHandle);
}
