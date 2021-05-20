using System.Collections.Generic;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Trash.Config;
using Trash.Radarr.CustomFormat.Models;

namespace Trash.Radarr.CustomFormat.Api
{
    internal class CustomFormatService : ICustomFormatService
    {
        private readonly IConfigurationProvider _configProvider;

        public CustomFormatService(IConfigurationProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public async Task<List<JObject>> GetCustomFormats()
        {
            return await BaseUrl()
                .AppendPathSegment("customformat")
                .GetJsonAsync<List<JObject>>();
        }

        public async Task CreateCustomFormat(ProcessedCustomFormatData cf)
        {
            var response = await BaseUrl()
                .AppendPathSegment("customformat")
                .PostJsonAsync(cf.Json)
                .ReceiveJson<JObject>();

            cf.SetCache((int) response["id"]);
        }

        public async Task UpdateCustomFormat(ProcessedCustomFormatData cf)
        {
            await BaseUrl()
                .AppendPathSegment($"customformat/{cf.GetCustomFormatId()}")
                .PutJsonAsync(cf.Json)
                .ReceiveJson<JObject>();
        }

        public async Task DeleteCustomFormat(int customFormatId)
        {
            await BaseUrl()
                .AppendPathSegment($"customformat/{customFormatId}")
                .DeleteAsync();
        }

        private string BaseUrl()
        {
            return _configProvider.ActiveConfiguration.BuildUrl();
        }
    }
}