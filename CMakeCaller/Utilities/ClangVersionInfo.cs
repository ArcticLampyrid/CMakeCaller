using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace QIQI.CMakeCaller.Utilities
{
    internal class ClangVersionInfo
    {
        private static readonly Regex VersionMatcher = new Regex(@"(?:Apple LLVM|Apple clang|clang)\s+version\s+([^\s-]+)[^\r\n]*");
        private static readonly Regex TargetMatcher = new Regex(@"Target:\s+([^\r\n]*)");
        private static readonly Regex ThreadModelMatcher = new Regex(@"Thread model:\s+([^\r\n]*)");
        private static readonly Regex InstalledDirMatcher = new Regex(@"InstalledDir:\s+([^\r\n]*)");

        public string FullVersion {get;set;}
        public string Version { get; set; }
        public string Target { get; set; }
        public string ThreadModel { get; set; }
        public string InstalledDir { get; set; }
        public static ClangVersionInfo GetFrom(string fileName)
        {
            using (var process = Process.Start(new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = "-v",
                CreateNoWindow = true,
                RedirectStandardError = true
            }))
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return ParseFrom(process.StandardError.ReadToEnd());
                }
            }
            return null;
        }
    
        public static ClangVersionInfo ParseFrom(string fullInfo)
        {
            var versionMatch = VersionMatcher.Match(fullInfo);
            var targetMatch = TargetMatcher.Match(fullInfo);
            var threadModelMatch = ThreadModelMatcher.Match(fullInfo);
            var installedDirMacth = InstalledDirMatcher.Match(fullInfo);
            return new ClangVersionInfo()
            {
                FullVersion = versionMatch.Value,
                Version = versionMatch.Success ? versionMatch.Groups[1].Value : string.Empty,
                Target = targetMatch.Success ? targetMatch.Groups[1].Value : string.Empty,
                ThreadModel = threadModelMatch.Success ? threadModelMatch.Groups[1].Value : string.Empty,
                InstalledDir = installedDirMacth.Success ? installedDirMacth.Groups[1].Value : string.Empty
            };
        }
    }
}
