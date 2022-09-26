using System.Text.Json.Serialization;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeGeneratorInfo
    {
        public string Name { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Toolset { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Platform { get; set; }
    }
}