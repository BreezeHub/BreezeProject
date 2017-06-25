using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NBitcoin;
using NBitcoin.RPC;

namespace Breeze.BreezeD
{
    public class TumblerApiAccess
    {
        private string _baseUrl = null;
        private HttpClient _client = null;

        public TumblerApiAccess(string baseUrl)
        {
            _baseUrl = baseUrl;
            _client = new HttpClient();
        }

        public async Task<string> GetParameters()
        {
            _client.DefaultRequestHeaders.Accept.Clear();

            var paramTask = _client.GetStringAsync(_baseUrl + "tumblers/0/parameters");

            return await paramTask;
        }
    }
}
