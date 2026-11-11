using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Windows DPAPI (CryptProtectData), current-user scope: the encrypted bytes can only be decrypted by the same Windows
    /// account on the same PC. Called directly through crypt32.dll because Unity's Mono has no ProtectedData.
    /// </summary>
    public static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr mem);

        private const int UiForbidden = 0x1;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VhvrSteamFrame.ServerPasswords.v1");

        public static byte[] Protect(byte[] data) => Run(data, protect: true);

        public static byte[] Unprotect(byte[] data) => Run(data, protect: false);

        private static byte[] Run(byte[] data, bool protect)
        {
            var input = Alloc(data);
            var entropy = Alloc(Entropy);
            var output = new DataBlob();
            try
            {
                bool ok = protect
                    ? CryptProtectData(ref input, "VhvrSteamFrame", ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                Free(input);
                Free(entropy);
                if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
            }
        }

        private static DataBlob Alloc(byte[] bytes)
        {
            var blob = new DataBlob { cbData = bytes.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, bytes.Length)) };
            Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
            return blob;
        }

        private static void Free(DataBlob blob)
        {
            if (blob.pbData == IntPtr.Zero) return;
            for (int i = 0; i < blob.cbData; i++) Marshal.WriteByte(blob.pbData, i, 0); // don't leave plaintext in freed memory
            Marshal.FreeHGlobal(blob.pbData);
        }
    }
}
