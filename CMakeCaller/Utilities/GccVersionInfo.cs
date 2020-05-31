using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QIQI.CMakeCaller.Utilities
{
    internal class GccVersionInfo
    {
        private static readonly Regex VersionMatcher = new Regex(@"gcc\s+version\s+(\S*)\s+[^\r\n]*");
        private static readonly Regex TargetMatcher = new Regex(@"Target:\s+([^\r\n]*)");
        private static readonly Regex ThreadModelMatcher = new Regex(@"Thread model:\s+([^\r\n]*)");

        public string FullVersion { get; set; }
        public string Version { get; set; }
        public string Target { get; set; }
        public string ThreadModel { get; set; }
        public static GccVersionInfo GetFrom(string fileName)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = fileName,
                Arguments = "-v",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c \"chcp 65001 && \"{fileName}\" -v\"";
            }
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return ParseFrom(process.StandardError.ReadToEnd());
                }
            }
            return null;
        }

        public static GccVersionInfo ParseFrom(string fullInfo)
        {
            var versionMatch = VersionMatcher.Match(fullInfo);
            var targetMatch = TargetMatcher.Match(fullInfo);
            var threadModelMatch = ThreadModelMatcher.Match(fullInfo);
            return new GccVersionInfo()
            {
                FullVersion = versionMatch.Value,
                Version = versionMatch.Success ? versionMatch.Groups[1].Value : string.Empty,
                Target = targetMatch.Success ? targetMatch.Groups[1].Value : string.Empty,
                ThreadModel = threadModelMatch.Success ? threadModelMatch.Groups[1].Value : string.Empty,
            };
        }
    }
}
