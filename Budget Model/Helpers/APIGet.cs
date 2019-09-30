using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Helpers
{
    class APIGet
    {
        public static string GetAPIdata(string url)
        {
            using (var httpClient = new HttpClient())
            {
                //httpClient.DefaultRequestHeaders.Add(RequestConstants.UserAgent, RequestConstants.UserAgentValue);
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var response = httpClient.GetStringAsync(new Uri(url)).Result;

                return response;
            }
        }
    }
}
