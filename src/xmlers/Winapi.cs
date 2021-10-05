using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace xmlers
{
    class Winapi
    {
        //----------------------------------------------------------
        // 証明書表示
        //----------------------------------------------------------
        internal static void showcert(IntPtr hdl, string title, byte[][] bcerts)
        {
            X509Certificate2Collection certs = new X509Certificate2Collection();
            foreach (byte[] bcert1 in bcerts)
            {
                certs.Add(new X509Certificate2(bcert1));
            }
            IntPtr hstore = CertOpenStore(CERT_STORE_PROV_MEMORY, 0, IntPtr.Zero, 0, null);
            X509Store store = new X509Store(hstore);
            store.AddRange(certs);
            var extraStoreArray = new[] { store.StoreHandle };
            var extraStoreArrayHandle = GCHandle.Alloc(
                extraStoreArray, GCHandleType.Pinned);
            var extraStorePointer = extraStoreArrayHandle.AddrOfPinnedObject();
            try
            {
                var viewInfo = new CRYPTUI_VIEWCERTIFICATE_STRUCT();
                viewInfo.hwndParent = hdl;
                viewInfo.dwSize = Marshal.SizeOf(viewInfo);
                viewInfo.pCertContext = certs[0].Handle;
                viewInfo.szTitle = title;
                viewInfo.nStartPage = 0;
                viewInfo.cStores = 1;
                viewInfo.rghStores = extraStorePointer;
                var fPropertiesChanged = false;
                CryptUIDlgViewCertificate(ref viewInfo, ref fPropertiesChanged);
            }
            catch (Exception) { }
        }
        //----------------------------------------------------------------------
        // PInvoke
        //----------------------------------------------------------------------
        [DllImport("CRYPT32.DLL", EntryPoint = "CertOpenStore", SetLastError = true)]
        static extern IntPtr CertOpenStore(int storeProvider, int encodingType,
            IntPtr hcryptProv, int flags, String pvPara);
        internal const int CERT_STORE_PROV_MEMORY = 2;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CRYPTUI_VIEWCERTIFICATE_STRUCT
        {
            public int dwSize;
            public IntPtr hwndParent;
            public int dwFlags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public String szTitle;
            public IntPtr pCertContext;
            public IntPtr rgszPurposes;
            public int cPurposes;
            public IntPtr pCryptProviderData;
            public Boolean fpCryptProviderDataTrustedUsage;
            public int idxSigner;
            public int idxCert;
            public Boolean fCounterSigner;
            public int idxCounterSigner;
            public int cStores;
            public IntPtr rghStores;
            public int cPropSheetPages;
            public IntPtr rgPropSheetPages;
            public int nStartPage;
        }
        [DllImport("CryptUI.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CryptUIDlgViewCertificate(ref CRYPTUI_VIEWCERTIFICATE_STRUCT pCertViewInfo, ref bool pfPropertiesChanged);
    }
}
