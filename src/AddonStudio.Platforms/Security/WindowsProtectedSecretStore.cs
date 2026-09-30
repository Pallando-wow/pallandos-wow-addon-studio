using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using AddonStudio.Application.Settings;

namespace AddonStudio.Platforms.Security;

public sealed class WindowsProtectedSecretStore(
    string? rootDirectory = null) : ILocalSecretStore
{
    private const int CryptProtectUiForbidden = 0x1;

    private readonly string rootDirectory =
        rootDirectory ??
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PallandosWowAddonStudio",
            "secrets");

    public string? Load(string key)
    {
        var path = GetPath(key);

        if (!File.Exists(path))
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Protected Studio secrets currently require Windows.");
        }

        var encrypted = File.ReadAllBytes(path);

        if (encrypted.Length == 0)
        {
            return null;
        }

        var clear = Unprotect(encrypted);
        return Encoding.UTF8.GetString(clear);
    }

    public void Save(
        string key,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Protected Studio secrets currently require Windows.");
        }

        Directory.CreateDirectory(rootDirectory);

        var clear =
            Encoding.UTF8.GetBytes(value);
        var encrypted =
            Protect(clear);

        File.WriteAllBytes(
            GetPath(key),
            encrypted);
    }

    public void Delete(string key)
    {
        var path = GetPath(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteAll()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(
                rootDirectory,
                recursive: true);
        }
    }

    private string GetPath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var safeName =
            string.Concat(
                key.Select(character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_'
                        ? character
                        : '_'));

        return Path.Combine(
            rootDirectory,
            safeName + ".bin");
    }

    private static byte[] Protect(
        byte[] clear) =>
        Transform(
            clear,
            protect: true);

    private static byte[] Unprotect(
        byte[] encrypted) =>
        Transform(
            encrypted,
            protect: false);

    private static byte[] Transform(
        byte[] input,
        bool protect)
    {
        var inputPointer =
            Marshal.AllocHGlobal(input.Length);

        try
        {
            Marshal.Copy(
                input,
                0,
                inputPointer,
                input.Length);

            var inputBlob =
                new DataBlob
                {
                    Size = input.Length,
                    Data = inputPointer
                };

            DataBlob outputBlob;

            var success =
                protect
                    ? CryptProtectData(
                        ref inputBlob,
                        null,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob)
                    : CryptUnprotectData(
                        ref inputBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);

            if (!success)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }

            try
            {
                var output =
                    new byte[outputBlob.Size];

                Marshal.Copy(
                    outputBlob.Data,
                    output,
                    0,
                    outputBlob.Size);

                return output;
            }
            finally
            {
                if (outputBlob.Data != IntPtr.Zero)
                {
                    LocalFree(
                        outputBlob.Data);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(
                inputPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;

        public IntPtr Data;
    }

    [DllImport(
        "Crypt32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport(
        "Crypt32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr LocalFree(
        IntPtr memory);
}
