using BlackFox.VsWhere;
using QIQI.CMakeCaller.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeKit
    {
        public delegate void ModifyStartInfo(ProcessStartInfo x);
        private CMakeKitInfo kitInfo;
        public string Name => kitInfo.Name;
        public CMakeKit(CMakeKitInfo kitInfo)
        {
            this.kitInfo = kitInfo ?? throw new ArgumentNullException(nameof(kitInfo));
        }
        public void ApplyKitEnv(StringDictionary envVars)
        {
            if (!string.IsNullOrEmpty(kitInfo.VSInstanceId) && !string.IsNullOrEmpty(kitInfo.VSArch))
            {
                var vsEnv = CMakeKitsScanner.VarsForVSInstance(kitInfo.VSInstanceId, kitInfo.VSArch);
                foreach (var item in vsEnv)
                {
                    envVars[item.Key] = item.Value;
                }
            }
            if (kitInfo.AdditionalPaths != null)
            {
                var pathEnvName = envVars.ContainsKey("Path") ? "Path" : "PATH";
                var pathSet = new HashSet<string>(envVars[pathEnvName].Split(PathUtils.PathEnvSeparator));
                var pathsWaitingToAdd = kitInfo.AdditionalPaths.Where(x => !pathSet.Contains(x));
                if (!envVars.ContainsKey(pathEnvName))
                {
                    envVars[pathEnvName] = "";
                }
                if (envVars[pathEnvName].EndsWith(PathUtils.PathEnvSeparator.ToString()))
                {
                    envVars[pathEnvName] += string.Join("", pathsWaitingToAdd.Select(x => x + PathUtils.PathEnvSeparator));
                }
                else
                {
                    envVars[pathEnvName] += string.Join("", pathsWaitingToAdd.Select(x => PathUtils.PathEnvSeparator + x));
                }
            }
        }
        public Process StartConfigure(CMakeEnv cmakeEnv, string pathToSource, string pathToBuild,
            Dictionary<string, CMakeSetting> userSettings = null, ModifyStartInfo modifyStartInfo = null)
        {
            var settings = new Dictionary<string, CMakeSetting>();
            if (kitInfo.PreferredGenerator != null)
            {
                if (kitInfo.PreferredGenerator.Name != null)
                {
                    settings["CMAKE_GENERATOR"] = new CMakeSetting(kitInfo.PreferredGenerator.Name);
                }
                if (kitInfo.PreferredGenerator.Toolset != null)
                {
                    settings["CMAKE_GENERATOR_TOOLSET"] = new CMakeSetting(kitInfo.PreferredGenerator.Toolset);
                }
                if (kitInfo.PreferredGenerator.Platform != null)
                {
                    settings["CMAKE_GENERATOR_PLATFORM"] = new CMakeSetting(kitInfo.PreferredGenerator.Platform);
                }
            }
            if (kitInfo.Compilers != null)
            {
                foreach (var compiler in kitInfo.Compilers)
                {
                    settings[$"CMAKE_{compiler.Key}_COMPILER"] = new CMakeSetting(compiler.Value, "FILEPATH");
                }
            }
            if (!string.IsNullOrEmpty(kitInfo.ToolchainFile))
            {
                settings["CMAKE_TOOLCHAIN_FILE"] = new CMakeSetting(kitInfo.ToolchainFile, "FILEPATH");
            }
            if (userSettings != null)
            {
                foreach (var userSetting in userSettings)
                {
                    settings[userSetting.Key] = userSetting.Value;
                }
            }
            var settingsArgs = string.Join(" ", settings.Select(x =>
                x.Value.Type == null ? 
                $"\"-D{x.Key}={x.Value.Value}\"" :
                $"\"-D{x.Key}:{x.Value.Type}={x.Value.Value}\""));
            var startInfo = new ProcessStartInfo()
            {
                FileName = cmakeEnv.CMakeBin,
                Arguments = $"-S \"{Path.GetFullPath(pathToSource)}\" -B \"{Path.GetFullPath(pathToBuild)}\" {settingsArgs}"
            };
            ApplyKitEnv(startInfo.EnvironmentVariables);
            modifyStartInfo?.Invoke(startInfo);
            return Process.Start(startInfo);
        }
        public Process StartBuild(CMakeEnv cmakeEnv, string pathToBuild,
            CMakeBuildConfig config = null, ModifyStartInfo modifyStartInfo = null)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = cmakeEnv.CMakeBin,
                Arguments = $"--build \"{Path.GetFullPath(pathToBuild)}\""
            };
            if (config?.Target != null)
            {
                var targetArg = string.Join(" ", config.Target.Select(x => $"\"{x}\""));
                startInfo.Arguments += $" --target {targetArg}";
            }
            if (config?.Config != null)
            {
                startInfo.Arguments += $" --config {config.Config}";
            }
            if (config != null && config.CleanFirst)
            {
                startInfo.Arguments += $" --clean-first";
            }
            ApplyKitEnv(startInfo.EnvironmentVariables);
            modifyStartInfo?.Invoke(startInfo);
            return Process.Start(startInfo);
        }
    }
}
