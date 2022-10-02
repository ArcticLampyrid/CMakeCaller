using QIQI.CMakeCaller;
using QIQI.CMakeCaller.Kits;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QIQI.CMakeCallerTester
{
    class Program
    {
        static void ModifyStartInfo(ProcessStartInfo x)
        {
            //Nothing to do here.
        }
        static int Main(string[] args)
        {
            bool hasError = false;
            var testProjectPath = args.Length > 0 ? args[0] : null;
            Console.WriteLine($"CMake Bin: {CMakeEnv.DefaultInstance.CMakeBin}");
            List<CMakeKitInfo> kitInfos = new List<CMakeKitInfo>();
            foreach (var kitInfo in CMakeKitsScanner.ScanAllKits())
            {
                kitInfos.Add(kitInfo);
                Console.WriteLine($"Found Kit: {kitInfo.Name}");
            }
            CMakeKitsController.SetKits(kitInfos);
            if (!string.IsNullOrEmpty(testProjectPath))
            {
                foreach (var kitInfo in kitInfos)
                {
                    var kit = new CMakeKit(kitInfo);
                    if (kit.Name.Contains("arm") 
                        && (kit.Name.Contains("Visual Studio 2015") || kit.Name.Contains("Visual Studio 2013") || kit.Name.Contains("Visual Studio 2012")))
                    {
                        // Compiling Desktop applications for the ARM platform is not supported without tricks
                        // Just skip it
                        continue;
                    }
                    Console.WriteLine($"Test Kit: {kitInfo.Name}");
                    var sourcePath = testProjectPath;
                    var buildPath = Path.Combine(testProjectPath, "bin", kitInfo.Name);
                    if (Directory.Exists(buildPath))
                    {
                        Directory.Delete(buildPath, true);
                    }
                    using (var process = kit.StartConfigure(CMakeEnv.DefaultInstance, sourcePath, buildPath, default(CMakeConfigureConfig), ModifyStartInfo))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            Console.WriteLine($"    *Failed to configure the project");
                            hasError = true;
                            continue;
                        }
                    }
                    using (var process = kit.StartBuild(CMakeEnv.DefaultInstance, buildPath, null, ModifyStartInfo))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            Console.WriteLine($"    *Failed to build the project");
                            hasError = true;
                            continue;
                        }
                    }
                    Console.WriteLine($"    Done");
                }
            }
            return hasError ? 1 : 0;
        }
    }
}
