using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Breeze.TumbleBit.Client.Services
{
    public class Hex
    {
        public string txid { get; set; }
        public string hex { get; set; }
    }

    public class TransactionHex
    {
        public bool success { get; set; }
        public List<Hex> hex { get; set; }
    }
    
    public enum SmartBitResultState
    {
        Success,
        Failure,
        ConnectionError,
        UnexpectedResponse
    }
    public class SmartBitResult
    {
        public SmartBitResultState State { get; set; }
        public string AdditionalInformation { get; set; }
        public string TransactionHex { get; set; }
    }
    public class SmartBitApi
    {
        private static readonly SemaphoreSlim Sem = new SemaphoreSlim(3, 3);
        private static readonly HttpClient HttpClient = new HttpClient();
        private string BaseUrl => Network == Network.Main
            ? "https://api.smartbit.com.au/v1/"
            : "https://testnet-api.smartbit.com.au/v1/";

        public Network Network { get; }
        public SmartBitApi(Network network)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            if(network != Network.TestNet && network != Network.Main)
            {
                throw new ArgumentException($"{nameof(network)} can only be {Network.TestNet} or {Network.Main}");
            }
        }

        public async Task<SmartBitResult> PushTx(Transaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            
            var post = $"{BaseUrl}blockchain/pushtx";

            var content = new StringContent(new JObject(new JProperty("hex", transaction.ToHex())).ToString(), Encoding.UTF8,
                "application/json");
            HttpResponseMessage smartBitResponse = null;
            await Sem.WaitAsync().ConfigureAwait(false);
            try
            {
                smartBitResponse = await HttpClient.PostAsync(post, content).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = ex.ToString()
                };
            }
            finally
            {
                Sem.SafeRelease();
            }
            if(smartBitResponse == null)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = $"{nameof(smartBitResponse)} is null"
                };
            }
            if (smartBitResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = $"Server answered with {smartBitResponse.StatusCode}"
                };
            }

            try
            {
                string response = await smartBitResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(response);

                var result = new SmartBitResult
                {
                    AdditionalInformation = response
                };
                if (json.Value<bool>("success"))
                {
                    result.State = SmartBitResultState.Success;
                }
                else
                {
                    result.State = SmartBitResultState.Failure;
                }
                return result;
            }
            catch(Exception ex)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.UnexpectedResponse,
                    AdditionalInformation = ex.ToString()
                };
            }
        }
        
        public async Task<SmartBitResult> GetTransaction(string txId)
        {
            if (txId == null) throw new ArgumentNullException(nameof(txId));
            
            var get = $"{BaseUrl}blockchain/tx/" + txId + "/hex";

            HttpResponseMessage smartBitResponse = null;
            await Sem.WaitAsync().ConfigureAwait(false);
            try
            {
                smartBitResponse = await HttpClient.GetAsync(get).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = ex.ToString()
                };
            }
            finally
            {
                Sem.SafeRelease();
            }
            if(smartBitResponse == null)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = $"{nameof(smartBitResponse)} is null"
                };
            }
            if (smartBitResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.Failure,
                    TransactionHex = null
                };
            }
            if (smartBitResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.ConnectionError,
                    AdditionalInformation = $"Server answered with {smartBitResponse.StatusCode}"
                };
            }

            try
            {
                string response = await smartBitResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var txResponse = JsonConvert.DeserializeObject<TransactionHex>(response);
                
                var result = new SmartBitResult
                {
                    AdditionalInformation = response
                };
                if (txResponse.success)
                {
                    result.State = SmartBitResultState.Success;
                    result.TransactionHex = txResponse.hex.First().hex;
                }
                else
                {
                    result.State = SmartBitResultState.Failure;
                    result.TransactionHex = null;
                }
                
                return result;
            }
            catch(Exception ex)
            {
                return new SmartBitResult
                {
                    State = SmartBitResultState.UnexpectedResponse,
                    AdditionalInformation = ex.ToString()
                };
            }
        }
    }
}
