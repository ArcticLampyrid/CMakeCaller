using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BlackFox.VsWhere;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace QIQI.CMakeCaller.Kits
{
    public static class CMakeKitsController
    {
        public static string CacheFileV1 { get; }
        static CMakeKitsController()
        {
            string localAppData;
            try
            {
                localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData, 
                    Environment.SpecialFolderOption.Create);
            }
            catch (Exception)
            {
                localAppData = Path.GetTempPath(); //fallback
            }
            var cacheFolder = Path.Combine(localAppData, ".QIQI.CMakeCaller");
            Directory.CreateDirectory(cacheFolder);
            CacheFileV1 = Path.Combine(cacheFolder, "kits-cache-v1.json");
        }
        public static ReadOnlyCollection<CMakeKitInfo> GetKits()
        {
            List<CMakeKitInfo> kits;
            try
            {
                using (var stream = File.OpenRead(CacheFileV1))
                {
                    kits = JsonSerializer.Deserialize<List<CMakeKitInfo>>(stream);
                }
            }
            catch (Exception)
            {
                kits = null;
            }
            if (kits == null)
            {
                kits = new List<CMakeKitInfo>();
            }
            return kits.AsReadOnly();
        }
        public static void SetKits(IEnumerable<CMakeKitInfo> kits)
        {
            try
            {
                using (var stream = File.Open(CacheFileV1, FileMode.Create))
                {
                    JsonSerializer.Serialize(stream, kits, new JsonSerializerOptions()
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
            }
            catch (Exception)
            {
            }
        }
        public static void ScanKits()
        {
            SetKits(CMakeKitsScanner.ScanAllKits());
        }
        public static Task ScanKitsAsync()
        {
            VsInstances.GetAll(); //Init on main thread
            return Task.Run(ScanKits);
        }
    }
}
