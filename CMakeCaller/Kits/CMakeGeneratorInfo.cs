using Newtonsoft.Json;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeGeneratorInfo
    {
        public string Name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Toolset { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Platform { get; set; }
    }
}