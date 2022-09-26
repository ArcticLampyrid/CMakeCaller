using BlackFox.VsWhere;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace QIQI.CMakeCaller.Kits
{
    public static class CMakeKitsController
    {
        public static string CacheFileV1 { get; }
        static CMakeKitsController()
        {
            string configPath;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    configPath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                    configPath = Path.Combine(configPath, $"{nameof(QIQI)}.{nameof(CMakeCaller)}");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Application Support");
                    configPath = Path.Combine(configPath, $"{nameof(QIQI)}.{nameof(CMakeCaller)}");
                }
                else
                {
                    // Windows or others
                    configPath = Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData,
                        Environment.SpecialFolderOption.Create);
                    configPath = Path.Combine(configPath, $"{nameof(QIQI)}.{nameof(CMakeCaller)}", "Config");
                }
                Directory.CreateDirectory(configPath);
            }
            catch (Exception)
            {
                // fallback
                Debug.WriteLine("Fallback: use temporary folder to store kits.");
                configPath = Path.Combine(Path.GetTempPath(), $"{nameof(QIQI)}.{nameof(CMakeCaller)}", "Config");
                Directory.CreateDirectory(configPath);
            }
            CacheFileV1 = Path.Combine(configPath, "kits-cache-v1.json");
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
