using Newtonsoft.Json;
using QIQI.CMakeCaller.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
                kits = JsonConvert.DeserializeObject<List<CMakeKitInfo>>(File.ReadAllText(CacheFileV1, Encoding.UTF8));
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
                var content = JsonConvert.SerializeObject(kits, Formatting.Indented);
                File.WriteAllText(CacheFileV1, content, Encoding.UTF8);
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
            return TaskUtils.StartSTATask(ScanKits);
        }
    }
}
