using System;
using System.Collections.Generic;
using System.Text;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeBuildConfig
    {
        public List<string> Target { get; set; }
        public string Config { get; set; }
        public bool CleanFirst { get; set; } = false;
    }
}
