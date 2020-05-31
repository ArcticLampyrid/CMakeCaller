using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace QIQI.CMakeCaller.Kits
{
    public class CMakeKitInfo
    {
        public string Name { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string VSInstanceId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string VSArch { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ToolchainFile { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Compilers { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> AdditionalPaths { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CMakeGeneratorInfo PreferredGenerator { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
