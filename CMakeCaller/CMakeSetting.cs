using System;
using System.Collections.Generic;
using System.Text;

namespace QIQI.CMakeCaller
{
    public class CMakeSetting
    {
        public CMakeSetting(string value, string type = null)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Type = type;
        }

        public string Type { get; set; }
        public string Value { get; set; }
    }
}
