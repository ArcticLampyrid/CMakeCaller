using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BlackFox.VsWhere;
using System.Text.RegularExpressions;
using QIQI.CMakeCaller.Utilities;

namespace QIQI.CMakeCaller
{
    public class CMakeEnv
    {
        public string CMakeBin { get; }
        public static CMakeEnv DefaultInstance = null;

        public CMakeEnv(string bin)
        {
            if (!File.Exists(bin))
            {
                throw new ArgumentException(nameof(bin));
            }
            CMakeBin = bin;
        }
        static CMakeEnv()
        {
            var bin = FindCMake();
            if (!string.IsNullOrEmpty(bin))
            {
                DefaultInstance = new CMakeEnv(bin);
            }
        }

        private static string FindCMake()
        {
            var methods = new Func<string>[]
            {
                () => PathUtils.Which("cmake"),
                () => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? PathUtils.GuessPath(new string[] {
                    "%ProgramW6432%\\CMake\\bin\\cmake.exe",
                    "%ProgramFiles%\\CMake\\bin\\cmake.exe",
                    "%ProgramFiles(x86)%\\CMake\\bin\\cmake.exe"
                }) : null,
                () => PathUtils.GuessPath(VsInstances.GetAll().Select(x => Path.Combine(x.InstallationPath,"Common7","IDE","CommonExtensions","Microsoft","CMake","CMake","bin","cmake.exe")))
            };
            var result = methods.Select(x =>
            {
                try
                {
                    return x();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
                {
                    return null;
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }).FirstOrDefault(x => x != null);
            if (result != null)
            {
                try
                {
                    result = Path.GetFullPath(result);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
                {
                    result = null;
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
            return result;
        }
    }
}
