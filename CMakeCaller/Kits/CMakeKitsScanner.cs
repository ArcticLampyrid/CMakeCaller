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
        private static readonly string[] MSVCEnvironmentVariables = new string[] {
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
        private static readonly string[] MSVCHostArches = new string[] { "x86", "x64" };
        private static readonly string[] MSVCTargetArches = new string[] { "x86", "x64", "arm", "arm64" };
        private static readonly Dictionary<int, string> VSGenerators = new Dictionary<int, string> {
            { 10, "Visual Studio 10 2010" },
            { 11, "Visual Studio 11 2012" },
            { 12, "Visual Studio 12 2013" },
            { 14, "Visual Studio 14 2015" },
            { 15, "Visual Studio 15 2017" },
            { 16, "Visual Studio 16 2019" },
            { 17, "Visual Studio 17 2022" }
        };

        internal static Dictionary<string, string> VarsForVSInstance(string instanceId, string vsArch)
        {
            VsSetupInstance vsInstance = null;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                vsInstance = VsInstances.GetAllWithLegacy().FirstOrDefault();
            }
            vsInstance = vsInstance ?? VsInstances.GetAllWithLegacy().FirstOrDefault(x => x.InstanceId == instanceId);
            return VarsForVSInstance(vsInstance, vsArch);
        }
        internal static bool ArchTestForVSInstance(VsSetupInstance inst, string vsArch)
        {
            try
            {
                return VarsForVSInstance(inst, vsArch).Count != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool GetActiveFileForVSInstance(VsSetupInstance inst, string vsArch, out string commonDir, out string devbat, out string activeArgs)
        {
            commonDir = Path.Combine(inst.InstallationPath, "Common7", "Tools");
            var installationVersion = new Version(inst.InstallationVersion);
            var amd64StyleVSArch = vsArch.Replace("x64", "amd64");
            if (installationVersion.Major < 15
                && !File.Exists(Path.Combine(inst.InstallationPath, "Common7", "IDE", "devenv.exe")))
            {
                // https://stackoverflow.com/a/46015781/5549179
                var instWithVCVersion = VsInstances.GetCompleted(true)
                    .First(x => new Version(x.InstallationVersion) >= new Version(15, 3));
                var devbatWithVCVersion = Path.Combine(instWithVCVersion.InstallationPath, "VC", "Auxiliary", "Build", "vcvarsall.bat");
                if (File.Exists(devbatWithVCVersion)
                    && File.ReadAllText(devbatWithVCVersion, Encoding.UTF8).Contains("vcvars_ver"))
                {
                    // check whether the required platform is installed
                    if (!vsArch.Contains("arm")
                        || Directory.Exists(Path.Combine(inst.InstallationPath, "VC", "bin", amd64StyleVSArch)))
                    {
                        devbat = devbatWithVCVersion;
                        activeArgs = $"{amd64StyleVSArch} -vcvars_ver={inst.InstallationVersion}";
                        return true;
                    }
                }
                else
                {
                    devbat = string.Empty;
                    activeArgs = string.Empty;
                    return false;
                }
            }
            var devbatDir = installationVersion.Major < 15
                ? Path.Combine(inst.InstallationPath, "VC")
                : Path.Combine(inst.InstallationPath, "VC", "Auxiliary", "Build");
            activeArgs = installationVersion.Major < 15
                ? amd64StyleVSArch
                : vsArch;
            var vcvarsScript = "vcvarsall.bat";
            if (vsArch.Contains("arm"))
            {
                vcvarsScript = $"vcvars{amd64StyleVSArch}.bat";
                activeArgs = "";
            }
            devbat = Path.Combine(devbatDir, vcvarsScript);
            if (!File.Exists(devbat))
            {
                switch (vsArch)
                {
                    case "x86":
                        vcvarsScript = "vcvars32.bat";
                        break;
                    case "x64":
                        vcvarsScript = "vcvars64.bat";
                        break;
                    default:
                        return false;
                }
                activeArgs = "";
                devbat = Path.Combine(devbatDir, vcvarsScript);
            }
            return File.Exists(devbat);
        }

        internal static Dictionary<string, string> VarsForVSInstance(VsSetupInstance inst, string vsArch)
        {
            var result = new Dictionary<string, string>();
            if (inst == null)
            {
                return result;
            }
            var installationVersion = new Version(inst.InstallationVersion);
            if (GetActiveFileForVSInstance(inst, vsArch, out var commonDir, out var devbat, out var activeArgs))
            {
                var tempDirectory = Path.GetTempPath();
                Directory.CreateDirectory(tempDirectory);
                var envFileName = $"{Guid.NewGuid()}.env";
                var batFileName = $"{Guid.NewGuid()}.bat";
                var envFilePath = Path.Combine(tempDirectory, envFileName);
                var batFilePath = Path.Combine(tempDirectory, batFileName);
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
            if (!result.ContainsKey("INCLUDE"))
            {
                result.Clear();
            }
            if (!result.TryGetValue("VCINSTALLDIR", out var vcInstallDir))
            {
                result.Clear();
            }
            if (!Directory.Exists(vcInstallDir))
            {
                result.Clear();
            }
            if (result.Count != 0)
            {
                if (result.TryGetValue("VISUALSTUDIOVERSION", out var vsVersion))
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
                return Array.Empty<CMakeKitInfo>();
            }
            return VsInstances.GetAllWithLegacy().SelectMany(x => TryFromVSInstance(x));
        }

        public static IEnumerable<CMakeKitInfo> TryFromVSInstance(VsSetupInstance vsInstance)
        {
            foreach (var hostArch in MSVCHostArches)
            {
                foreach (var targetArch in MSVCTargetArches)
                {
                    var vsArch = KitHostTargetArch(hostArch, targetArch);
                    if (!ArchTestForVSInstance(vsInstance, vsArch))
                    {
                        continue;
                    }
                    yield return new CMakeKitInfo
                    {
                        Name = $"{VsDisplayName(vsInstance)} - {KitHostTargetArch(hostArch, targetArch)}",
                        VSInstanceId = vsInstance.InstanceId,
                        VSArch = KitHostTargetArch(hostArch, targetArch),
                        PreferredGenerator = FindVSGenerator(vsInstance, hostArch, targetArch)
                    };
                }
            }
        }

        private static CMakeGeneratorInfo FindNinja() 
        {
            var ninjaPath = PathUtils.Which("ninja");
            if (ninjaPath != null)
            {
                return new CMakeGeneratorInfo()
                {
                    Name = "Ninja"
                };
            }
            return null;
        }

        private static CMakeGeneratorInfo FindVSGenerator(VsSetupInstance inst, string hostArch, string targetArch)
        {
            var installationVersion = new Version(inst.InstallationVersion);
            if (VSGenerators.TryGetValue(installationVersion.Major, out var generatorName))
            {
                return new CMakeGeneratorInfo()
                {
                    Name = generatorName,
                    Platform = targetArch == "x86" ? "win32" : targetArch,
                    Toolset = $"host={hostArch}"
                };
            }
            return FindNinja() ?? new CMakeGeneratorInfo()
            {
                Name = "NMake Makefiles"
            };
        }

        public static IEnumerable<CMakeKitInfo> ScanClangKits()
        {
            var clangRegex = new Regex(@"^clang(-\d+(\.\d+(\.\d+)?)?)?(\.exe)?$", RegexOptions.CultureInvariant);
            var clangs = PathUtils.FindFilesInEnvironment(clangRegex);
            return clangs.SelectMany(clangFile => TryFromClang(clangFile));
        }

        public static IEnumerable<CMakeKitInfo> TryFromClang(string clangFile)
        {
            var clangxxFile = clangFile.Replace("clang", "clang++");
            var version = ClangVersionInfo.GetFrom(clangFile);
            if (version == null)
            {
                yield break;
            }
            if (version.Target.IndexOf("msvc") != -1)
            {
                // Clang targeting MSVC can't be used without MSVC Enviroment
                // These instances will be handled by using clang-cl.exe
                yield break;
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
            var toolSets = new HashSet<string>();
            return gccs.SelectMany(gccFile => TryFromGcc(gccFile, toolSets));
        }

        public static IEnumerable<CMakeKitInfo> TryFromGcc(string gccFile, HashSet<string> toolSets = null)
        {
            var gxxFile = gccFile.Replace("gcc", "g++");
            var version = GccVersionInfo.GetFrom(gccFile);
            if (version == null)
            {
                yield break;
            }
            if (toolSets != null)
            {
                if (toolSets.Contains(version.FullVersion))
                {
                    yield break;
                }
                toolSets.Add(version.FullVersion);
            }
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
            kitInfo.PreferredGenerator = FindNinja();
            if (kitInfo.PreferredGenerator == null
                && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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

        public static IEnumerable<CMakeKitInfo> ScanClangClKits()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Array.Empty<CMakeKitInfo>();
            }

            var vsInstances = VsInstances.GetAllWithLegacy();
            var clangClRegex = new Regex(@"^clang-cl.*$", RegexOptions.CultureInvariant);

            var searchPaths =
                PathUtils.GetSearchPaths()
                    .Concat(vsInstances.Select(x => Path.Combine(x.InstallationPath, "VC", "Tools", "Llvm", "bin")))
                    .Concat(new string[]
                    {
                        "%LLVM_ROOT%\\bin",
                        "%ProgramW6432%\\LLVM\\bin",
                        "%ProgramFiles%\\LLVM\\bin",
                        "%ProgramFiles(x86)%\\LLVM\\bin"
                    }.Select(PathUtils.NormalizePath))
                    .Distinct();
            var clangCls = PathUtils.FindFiles(clangClRegex, searchPaths);
            return clangCls.SelectMany(clangClFile => TryFromClangCl(clangClFile, vsInstances));
        }

        public static IEnumerable<CMakeKitInfo> TryFromClangCl(string clangClFile, IEnumerable<VsSetupInstance> vsInstances = null)
        {
            var version = ClangVersionInfo.GetFrom(clangClFile);
            if (version == null)
            {
                yield break;
            }
            if (vsInstances is null)
            {
                vsInstances = VsInstances.GetAllWithLegacy();
            }
            foreach (var vsInstance in vsInstances)
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
                    PreferredGenerator = FindNinja() ?? new CMakeGeneratorInfo()
                    {
                        Name = "NMake Makefiles"
                    }
                };
            }
        }
    }
}