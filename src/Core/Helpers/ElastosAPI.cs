using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Core.Helpers
{
    public class ElastosAPI
    {
        public async System.Threading.Tasks.Task<DIDResult> CreateDIDAsync()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("http://18.179.20.67:8080/api/1/did");
            string json = await response.Content.ReadAsStringAsync();
            DID did = JsonConvert.DeserializeObject<DID>(json);
            return did.result;
        }

        public async System.Threading.Tasks.Task<string> GetDIDAsync(string privateKey)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("http://18.179.20.67:8080/api/1/did/" + privateKey);
            string json = await response.Content.ReadAsStringAsync();
            JObject obj = JsonConvert.DeserializeObject<JObject>(json);
            return (string) obj["result"];
        }
    }
}
