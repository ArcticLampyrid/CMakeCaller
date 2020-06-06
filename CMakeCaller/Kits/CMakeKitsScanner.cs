using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using BlackFox.VsWhere;
using QIQI.CMakeCaller.Utilities;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeKitsScanner
    {
        public static IEnumerable<CMakeKitInfo> ScanAllKits()
        {
            return ScanVSKits()
                .Concat(ScanClangKits())
                .Concat(ScanGccKits())
                .Concat(ScanClangClKits());
        }
        private static string VsDisplayName(VsSetupInstance inst)
        {
            return inst.DisplayName;
        }
        private static string KitHostTargetArch(string hostArch, string targetArch)
        {
            return hostArch == targetArch ? hostArch : $"{hostArch}_{targetArch}";
        }
        private static string[] MSVCEnvironmentVariables = new string[] {
            "CL",
            "_CL_",
            "INCLUDE",
            "LIBPATH",
            "LINK",
            "_LINK_",
            "LIB",
            "PATH",
            "TMP",
            "FRAMEWORKDIR",
            "FRAMEWORKDIR64",
            "FRAMEWORKVERSION",
            "FRAMEWORKVERSION64",
            "UCRTCONTEXTROOT",
            "UCRTVERSION",
            "UNIVERSALCRTSDKDIR",
            "VCINSTALLDIR",
            "VCTARGETSPATH",
            "WINDOWSLIBPATH",
            "WINDOWSSDKDIR",
            "WINDOWSSDKLIBVERSION",
            "WINDOWSSDKVERSION",
            "VISUALSTUDIOVERSION"
        };

        internal static Dictionary<string, string> VarsForVSInstance(string instanceId, string vsArch)
        {
            VsSetupInstance vsInstance = null;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                vsInstance = VsInstances.GetAll().FirstOrDefault();
            }
            vsInstance = vsInstance ?? VsInstances.GetAll().FirstOrDefault(x => x.InstanceId == instanceId);
            return VarsForVSInstance(vsInstance, vsArch);
        }
        internal static Dictionary<string, string> VarsForVSInstance(VsSetupInstance inst, string vsArch)
        {
            var result = new Dictionary<string, string>();
            if (inst == null)
            {
                return result;
            }
            var commonDir = Path.Combine(inst.InstallationPath, "Common7", "Tools");
            var vcvarsScript = "vcvarsall.bat";
            if (vsArch.IndexOf("arm") != -1)
            {
                // For performance
                vcvarsScript = $"vcvars{vsArch.Replace("x64", "amd64")}.bat";
            }
            string devbat;
            var installationVersion = new Version(inst.InstallationVersion);
            if (installationVersion.Major < 15)
            {
                devbat = Path.Combine(inst.InstallationPath, "VC", vcvarsScript);
            }
            else
            {
                devbat = Path.Combine(inst.InstallationPath, "VC", "Auxiliary", "Build", vcvarsScript);
            }
            if (File.Exists(devbat))
            {
                var tempDirectory = Path.GetTempPath();
                Directory.CreateDirectory(tempDirectory);
                var envFileName = $"{Guid.NewGuid()}.env";
                var batFileName = $"{Guid.NewGuid()}.bat";
                var envFilePath = Path.Combine(tempDirectory, envFileName);
                var batFilePath = Path.Combine(tempDirectory, batFileName);
                var activeArgs = vsArch;
                if (installationVersion.Major < 15)
                {
                    activeArgs = activeArgs.Replace("x64", "amd64");
                }
                using (var batFile = File.CreateText(batFilePath))
                {
                    batFile.WriteLine("@echo off");
                    batFile.WriteLine("cd /d \"%~dp0\"");
                    batFile.WriteLine($"set \"VS{installationVersion.Major}0COMNTOOLS={commonDir}\"");
                    batFile.WriteLine($"call \"{devbat}\" {activeArgs} || exit");
                    batFile.WriteLine("cd /d \"%~dp0\"");
                    foreach (var envVarName in MSVCEnvironmentVariables)
                    {
                        batFile.WriteLine($"echo {envVarName}=%{envVarName}%>> {envFileName}");
                    }
                }

                using (var process = Process.Start(new ProcessStartInfo()
                {
                    FileName = batFilePath,
                    CreateNoWindow = true
                }))
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0 && File.Exists(envFilePath))
                    {
                        var lines = File.ReadAllLines(envFilePath);
                        foreach (var line in lines)
                        {
                            var pSeparator = line.IndexOf('=');
                            if (pSeparator < 0)
                            {
                                continue;
                            }
                            result[line.Substring(0, pSeparator).Trim()] = line.Substring(pSeparator + 1).Trim();
                        }
                    }
                }
                File.Delete(envFilePath);
                File.Delete(batFilePath);
            }
            if(!result.ContainsKey("INCLUDE"))
            {
                result.Clear();
            }
            if (result.Count != 0)
            {
                if(result.TryGetValue("VISUALSTUDIOVERSION", out var vsVersion))
                {
                    result[$"VS{vsVersion.Replace(".", "")}COMNTOOLS"] = commonDir;
                }
                result["CC"] = "cl.exe";
                result["CXX"] = "cl.exe";
            }
            return result;
        }

        public static IEnumerable<CMakeKitInfo> ScanVSKits()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield break;
            }

            var hostArches = new string[] { "x86", "x64" };
            var targetArches = new string[] { "x86", "x64", "arm", "arm64" };
            foreach (var vsInstance in VsInstances.GetAll())
            {
                foreach (var hostArch in hostArches)
                {
                    foreach (var targetArch in targetArches)
                    {
                        var vsArch = KitHostTargetArch(hostArch, targetArch);
                        var vsEnv = VarsForVSInstance(vsInstance, vsArch);
                        if (vsEnv.Count == 0)
                        {
                            continue;
                        }
                        yield return new CMakeKitInfo
                        {
                            Name = $"{VsDisplayName(vsInstance)} - {KitHostTargetArch(hostArch, targetArch)}",
                            VSInstanceId = vsInstance.InstanceId,
                            VSArch = KitHostTargetArch(hostArch, targetArch),
                            PreferredGenerator = new CMakeGeneratorInfo() 
                            {
                                Name = "NMake Makefiles"
                            }
                        };
                    }
                }
            }
        }

        public static IEnumerable<CMakeKitInfo> ScanClangKits()
        {
            var clangRegex = new Regex(@"^clang(-\d+(\.\d+(\.\d+)?)?)?(\.exe)?$", RegexOptions.CultureInvariant);
            foreach (var clangFile in PathUtils.FindFilesInEnvironment(clangRegex))
            {
                var clangxxFile = clangFile.Replace("clang", "clang++");
                var version = ClangVersionInfo.GetFrom(clangFile);
                if (version == null)
                {
                    continue;
                }
                if (version.Target.IndexOf("msvc") != -1)
                {
                    // Clang targeting MSVC can't be used without MSVC Enviroment
                    // These instances will be handled by using clang-cl.exe
                    continue;
                }
                var kitInfo = new CMakeKitInfo()
                {
                    Name = $"Clang {version.Version} ({version.Target})",
                    Compilers = new Dictionary<string, string>()
                    {
                        { "C", clangFile }
                    }
                };
                if (File.Exists(clangxxFile))
                {
                    kitInfo.Compilers["CXX"] = clangxxFile;
                }
                yield return kitInfo;
            }
        }

        public static IEnumerable<CMakeKitInfo> ScanGccKits()
        {
            var searchPaths = PathUtils.GetSearchPaths();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                searchPaths = searchPaths.Concat(new string[]
                {
                    "C:\\TDM-GCC-64\\bin",
                    "C:\\TDM-GCC-32\\bin"
                }.Select(PathUtils.NormalizePath));
            }
            searchPaths = searchPaths.Distinct();
            var gccRegex = new Regex(@"^((\w+-)*)gcc(-\d+(\.\d+(\.\d+)?)?)?(\.exe)?$", RegexOptions.CultureInvariant);
            var gccs = PathUtils.FindFiles(gccRegex, searchPaths);
            var toolSet = new HashSet<string>();
            foreach (var gccFile in gccs)
            {
                var gxxFile = gccFile.Replace("gcc", "g++");
                var version = GccVersionInfo.GetFrom(gccFile);
                if (version == null)
                {
                    continue;
                }
                if (toolSet.Contains(version.FullVersion))
                {
                    continue;
                }
                toolSet.Add(version.FullVersion);
                var kitInfo = new CMakeKitInfo()
                {
                    Name = $"GCC {version.Version} ({version.Target})",
                    Compilers = new Dictionary<string, string>()
                    {
                        { "C", gccFile }
                    }
                };
                if (File.Exists(gxxFile))
                {
                    kitInfo.Compilers["CXX"] = gxxFile;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    && version.Target.EndsWith("-mingw32"))
                {
                    var mingw32Make = Path.Combine(Path.GetDirectoryName(gccFile), "mingw32-make.exe");
                    if (File.Exists(mingw32Make))
                    {
                        kitInfo.AdditionalPaths = new List<string>() { Path.GetDirectoryName(mingw32Make) };
                        kitInfo.PreferredGenerator = new CMakeGeneratorInfo()
                        {
                            Name = "MinGW Makefiles"
                        };
                    }
                }
                yield return kitInfo;
            }
        }

        public static IEnumerable<CMakeKitInfo> ScanClangClKits()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield break;
            }

            var vsIntances = VsInstances.GetAll();
            var clangClRegex = new Regex(@"^clang-cl.*$", RegexOptions.CultureInvariant);

            var searchPaths =
                PathUtils.GetSearchPaths()
                    .Concat(vsIntances.Select(x => Path.Combine(x.InstallationPath, "VC", "Tools", "Llvm", "bin")))
                    .Concat(new string[]
                    {
                        "%LLVM_ROOT%\\bin",
                        "%ProgramW6432%\\LLVM\\bin",
                        "%ProgramFiles%\\LLVM\\bin",
                        "%ProgramFiles(x86)%\\LLVM\\bin"
                    }.Select(PathUtils.NormalizePath))
                    .Distinct();
            var clangCls = PathUtils.FindFiles(clangClRegex, searchPaths);
            foreach (var clangClFile in clangCls)
            {
                var version = ClangVersionInfo.GetFrom(clangClFile);
                if (version == null)
                {
                    continue;
                }
                foreach (var vsInstance in vsIntances)
                {
                    var vsArch = "x64";
                    if (version.Target != null && version.Target.IndexOf("i686-pc") != -1)
                    {
                        vsArch = "x86";
                    }
                    yield return new CMakeKitInfo()
                    {
                        Name = $"Clang {version.Version} for MSVC with {VsDisplayName(vsInstance)} ({vsArch})",
                        Compilers = new Dictionary<string, string>()
                        {
                            { "C", clangClFile },
                            { "CXX", clangClFile }
                        },
                        VSInstanceId = vsInstance.InstanceId,
                        VSArch = vsArch,
                        PreferredGenerator = new CMakeGeneratorInfo()
                        {
                            Name = "NMake Makefiles"
                        }
                    };
                }
            }
        }
    }
}
