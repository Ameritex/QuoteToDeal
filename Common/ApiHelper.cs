using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Quote_To_Deal.Common
{
    public class ApiHelper
    {

        public static async Task<T> Get<T>(string baseApi, string url, string apiKey)
        {
            RestClient client = new RestClient(baseApi);
            client.Timeout = Int32.MaxValue;
            RestRequest request = new RestRequest($"{url}", Method.GET);
            request.Timeout = Int32.MaxValue;
            request.AddHeader("Authorization", apiKey);
            var response = await client.ExecuteTaskAsync(request);
            if (response != null)
            {
                try
                {
                    //var abc = JsonConvert.DeserializeObject(response.Content);
                    return JsonConvert.DeserializeObject<T>(response.Content);
                }catch
                {
                    return default(T);
                }
            }
            else
            {
                return default(T);
            }
        }

        public static void DownloadFile(string url, string path)
        {
            var client = new RestClient(url);
            client.Timeout = 5 * 60 * 1000;
           
            RestRequest request = new RestRequest($"{url}", Method.GET);
            request.Timeout = 5 * 60 * 1000;
           
            byte[] response = client.DownloadData(request);
            File.WriteAllBytes(path, response);
        }
    }
}
