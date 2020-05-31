using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QIQI.CMakeCaller.Utilities
{
    internal static class PathUtils
    {
        private static readonly Regex EnvInWin32Path = new Regex(@"%([^%]*)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static ReadOnlyCollection<string> PathExt { get; } = Array.AsReadOnly(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            new string[] { "", ".exe", ".cmd", ".bat", ".com" } :
            new string[] { "" }
        );
        public static char PathEnvSeparator { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

        public static string NormalizePath(string path)
        {
            var result = path;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = EnvInWin32Path.Replace(result, x => {
                    try
                    {
                        return Environment.GetEnvironmentVariable(x.Groups[0].Value);
                    }
                    catch (Exception)
                    {
                        return x.Value;
                    }
                });
            }
            return result;
        }
        public static string GuessPath(IEnumerable<string> possiblePaths)
        {
            return possiblePaths?.Select(NormalizePath)?.FirstOrDefault(x => File.Exists(x));
        }
        public static IEnumerable<string> GetSearchPaths(EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        {
            return (Environment.GetEnvironmentVariable("PATH", target) ?? "").Split(PathEnvSeparator);
        }
        public static string GetFilePathInEnvironment(string fileName, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        {
            return GetSearchPaths(target)
                .Select(x => Path.Combine(x, fileName))
                .FirstOrDefault(x => File.Exists(x));
        }
        public static IEnumerable<string> FindFilesInEnvironment(Regex condition, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        {
            return FindFiles(condition, GetSearchPaths(target));
        }
        public static IEnumerable<string> FindFiles(Regex condition, IEnumerable<string> searchPaths)
        {
            return searchPaths
                .Where(x => Directory.Exists(x))
                .SelectMany(x => new DirectoryInfo(x).GetFiles())
                .Where(x => condition.IsMatch(x.Name))
                .Select(x => x.FullName);
        }
        public static string Which(string fileName)
        {
            foreach (var curExt in PathExt)
            {
                var curFileName = fileName + curExt;
                var result = GetFilePathInEnvironment(curFileName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
