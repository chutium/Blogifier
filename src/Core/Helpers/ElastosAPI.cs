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
        public DIDResult CreateDID()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://18.179.20.67:8080/api/1/did").Result;
            string json = response.Content.ReadAsStringAsync().Result;
            DID did = JsonConvert.DeserializeObject<DID>(json);
            return did.result;
        }

        public string GetDID(string privateKey)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://18.179.20.67:8080/api/1/did/" + privateKey).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            JObject obj = JsonConvert.DeserializeObject<JObject>(json);
            return (string) obj["result"];
        }

        public string SetDIDInfo(string privateKey, DIDInfoKey key, JObject info)
        {
            JObject json = JObject.Parse(@"{
                'privateKey': 'C740869D015E674362B1F441E3EDBE1CBCF4FE8B709AA1A77E5CCA2C92BAF99D',
                'settings': {
                    'privateKey': ''
                }
            }");
            json["settings"]["privateKey"] = privateKey;
            JObject settings = json["settings"] as JObject;
            settings.Add("info", JToken.Parse(@"{'" + key + "':" + JsonConvert.SerializeObject(info) + "}"));
            HttpClient client = new HttpClient();
            using (var response = client.PostAsJsonAsync("http://18.179.20.67:8080/api/1/setDidInfo", json).Result)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                JObject obj = JsonConvert.DeserializeObject<JObject>(result);
                return (string)obj["result"];
            }
        }

        public string GetDIDInfo(string txid, DIDInfoKey key)
        {
            JObject json = JObject.Parse($"{{\"txIds\":[\"{txid}\"], \"key\":\"{key}\"}}");
            HttpClient client = new HttpClient();
            using (var response = client.PostAsJsonAsync("http://18.179.20.67:8080/api/1/getDidInfo", json).Result)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                JObject obj = JsonConvert.DeserializeObject<JObject>(result);
                return obj["result"]["data"].ToString(Formatting.None);
            }
        }
    }
}
