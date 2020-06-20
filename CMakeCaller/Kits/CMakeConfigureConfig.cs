using System;
using System.Collections.Generic;
using System.Text;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeConfigureConfig
    {
        public Dictionary<string, CMakeSetting> UserSettings { get; set; }
        public bool NoWarnUnusedCli { get; set; } = true;
    }
}
