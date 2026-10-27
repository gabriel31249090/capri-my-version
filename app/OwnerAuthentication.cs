using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace CapriDesktop
{
    // Windows validates the password. It is never converted to a managed string,
    // persisted, sent to a model, or included in Chronicle.
    internal static class OwnerAuthentication
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CredentialInfo
        {
            internal int Size;
            internal IntPtr Parent;
            internal string Message;
            internal string Caption;
            internal IntPtr Banner;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern int CredUIPromptForWindowsCredentialsW(ref CredentialInfo info, int error,
            ref uint package, IntPtr input, uint inputSize, out IntPtr output, out uint outputSize,
            [MarshalAs(UnmanagedType.Bool)] ref bool save, uint flags);
        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredUnPackAuthenticationBufferW(uint flags, IntPtr buffer, uint size,
            IntPtr user, ref uint userLength, IntPtr domain, ref uint domainLength,
            IntPtr password, ref uint passwordLength);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LogonUserW(string user, string domain, IntPtr password,
            int logonType, int provider, out IntPtr token);
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        internal static string CurrentSid()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return identity.User == null ? null : identity.User.Value;
        }

        internal static string Verify(IWin32Window parent, string expectedSid)
        {
            if (string.IsNullOrEmpty(expectedSid) || CurrentSid() != expectedSid) return null;
            CredentialInfo info = new CredentialInfo
            {
                Size = Marshal.SizeOf(typeof(CredentialInfo)),
                Parent = parent == null ? IntPtr.Zero : parent.Handle,
                Caption = "CAPRI / Guardian",
                Message = "Confirme a senha da conta Windows proprietária para desbloquear a CAPRI. O PIN do Windows Hello não substitui a senha nesta verificação."
            };
            uint package = 0, blobSize = 0;
            IntPtr blob = IntPtr.Zero, user = IntPtr.Zero, domain = IntPtr.Zero, password = IntPtr.Zero, token = IntPtr.Zero;
            bool save = false;
            const int capacity = 1024;
            try
            {
                int result = CredUIPromptForWindowsCredentialsW(ref info, 0, ref package, IntPtr.Zero, 0,
                    out blob, out blobSize, ref save, 1); // CREDUIWIN_GENERIC: password credentials.
                if (result == 1223) return null;
                if (result != 0) throw new Win32Exception(result);
                if (blob == IntPtr.Zero || blobSize > 1024 * 1024) throw new InvalidOperationException("Resposta de autenticação inválida.");
                user = Marshal.AllocCoTaskMem(capacity * 2);
                domain = Marshal.AllocCoTaskMem(capacity * 2);
                password = Marshal.AllocCoTaskMem(capacity * 2);
                Zero(user, capacity * 2); Zero(domain, capacity * 2); Zero(password, capacity * 2);
                uint userLength = capacity, domainLength = capacity, passwordLength = capacity;
                if (!CredUnPackAuthenticationBufferW(0, blob, blobSize, user, ref userLength,
                    domain, ref domainLength, password, ref passwordLength))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                string userName = Marshal.PtrToStringUni(user);
                string domainName = Marshal.PtrToStringUni(domain);
                if (string.IsNullOrEmpty(userName)) return null;
                int split = userName.IndexOf('\\');
                if (split >= 0) { domainName = userName.Substring(0, split); userName = userName.Substring(split + 1); }
                if (userName.IndexOf('@') >= 0) domainName = null;
                else if (string.IsNullOrEmpty(domainName)) domainName = ".";
                if (!LogonUserW(userName, domainName, password, 3, 0, out token))
                    throw new InvalidOperationException("O Windows não confirmou as credenciais do proprietário.");
                using (WindowsIdentity authenticated = new WindowsIdentity(token))
                {
                    string sid = authenticated.User == null ? null : authenticated.User.Value;
                    return sid == expectedSid && CurrentSid() == expectedSid ? sid : null;
                }
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
                Release(user, capacity * 2); Release(domain, capacity * 2); Release(password, capacity * 2);
                Release(blob, (int)Math.Min(blobSize, 1024u * 1024u));
            }
        }

        private static void Zero(IntPtr pointer, int length)
        {
            if (pointer != IntPtr.Zero) for (int i = 0; i < length; i++) Marshal.WriteByte(pointer, i, 0);
        }
        private static void Release(IntPtr pointer, int length)
        {
            if (pointer == IntPtr.Zero) return;
            Zero(pointer, length);
            Marshal.FreeCoTaskMem(pointer);
        }
    }
}
