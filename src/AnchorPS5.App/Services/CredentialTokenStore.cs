using System.Runtime.InteropServices;
using System.Text;
using AnchorPS5.Core.GitHub;

namespace AnchorPS5.App.Services;

/// <summary>
/// Token de GitHub en el Administrador de credenciales de Windows (cifrado por el
/// sistema para el usuario actual), fuera de los JSON portables de la app.
/// </summary>
public sealed class CredentialTokenStore : ITokenStore
{
    private const string Target = "AnchorPS5/GitHub";
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public string? Read()
    {
        if (!CredRead(Target, CredTypeGeneric, 0, out var handle))
            return null;

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            return credential.CredentialBlobSize > 0
                ? Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2)
                : null;
        }
        finally
        {
            CredFree(handle);
        }
    }

    public void Save(string token)
    {
        var blob = Encoding.Unicode.GetBytes(token);
        var blobPointer = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPointer, blob.Length);
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = Target,
                UserName = "AnchorPS5",
                CredentialBlob = blobPointer,
                CredentialBlobSize = blob.Length,
                Persist = CredPersistLocalMachine,
            };
            if (!CredWrite(ref credential, 0))
                throw new IOException($"CredWrite: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeHGlobal(blobPointer);
        }
    }

    public void Delete() => CredDelete(Target, CredTypeGeneric, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
