using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;

namespace CCTriArb
{
    public class CKuCoin : CExchange
    {
        public CKuCoin(CStrategyServer server) : base(server)
        {
            BaseURL = "https://api.kucoin.com/v1/open/tick";
            Name = "KuCoin";
            getAccounts();
        }


        public override void cancel(string orderID)
        {
            throw new NotImplementedException();
        }

        public override void cancelAll()
        {
            throw new NotImplementedException();
        }

        public override void getAccounts()
        {
            //throw new NotImplementedException();
        }

        public override void pollOrders(object source, ElapsedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            var wc = new WebClient();
            wc.Headers.Add("user-agent", USER_AGENT);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            foreach (CProduct product in colProducts)
            {
                try
                {
                    String tickURL = BaseURL + "?symbol=" + product.Symbol;
                    var json = wc.DownloadString(tickURL);
                    dynamic tickData = JsonConvert.DeserializeObject(json);
                    product.Bid = tickData.data.buy;
                    product.Ask = tickData.data.sell;
                    product.Last = tickData.data.lastDealPrice;
                    product.Volume = tickData.data.volValue;

                    long numTicks = tickData.data.datetime;
                    var posixTime = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc);
                    var time = posixTime.AddMilliseconds(numTicks);
                    product.DtUpdate = time;

                    foreach (CStrategy strategy in product.colStrategy)
                    {
                        strategy.updateGUI();
                    }


                }
                catch (Exception ex)
                {
                    Server.AddLog(ex.Message);
                }
            }
        }

        public override async void trade(ServerType serverType, OrderSide? side, CProduct product, decimal size, decimal? price)
        {
            try
            {
                Uri baseAddress;
                switch (serverType)
                {
                    case ServerType.Debugging:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-proxy.com/");
                        break;

                    case ServerType.Mock:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-mock.com/");
                        break;

                    default:
                        baseAddress = new Uri("https://api.kucoin.com/");
                        break;
                }

                String API_KEY = Properties.Settings.Default.KUCOIN_API_KEY;
                String API_SECRET = Properties.Settings.Default.KUCOIN_API_SECRET;
                String endpoint = "/v1/KCS-BTC/order";  // API endpoint

                HttpClient httpClient = new HttpClient { BaseAddress = baseAddress };
                Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "amount", "1000" },
                    { "price", "0.1" },
                    //{ "symbol", "KTC-BTC" },
                    { "type", "BUY" }
                };
                HttpContent queryString = new FormUrlEncodedContent(parameters);
                String strQuery = "";
                foreach (String param in parameters.Keys)
                {
                    if (strQuery.Length > 0)
                        strQuery += "&";
                    strQuery += (param + "=" + parameters[param]);
                }

                //splice string for signing
                String nonce = CHelper.ConvertToUnixTimestamp().ToString();

                // 1517980619532 https://www.freeformatter.com/epoch-timestamp-to-date-converter.html

                String strForSign = endpoint + "/" + nonce + "/" + strQuery;
                String signatureStr = CHelper.Base64Encode(strForSign);
                String signatureResult = CHelper.hmacsha256(API_SECRET, strForSign);

                httpClient.DefaultRequestHeaders.Add("KC-API-KEY", API_KEY);
                httpClient.DefaultRequestHeaders.Add("KC-API-NONCE", nonce);
                httpClient.DefaultRequestHeaders.Add("KC-API-SIGNATURE", signatureResult);

                var response = await httpClient.PostAsync(baseAddress + endpoint, queryString).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Do something with response. Example get content:

                }

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Server.AddLog(responseContent);
            }
            catch (Exception ex)
            {
                Server.AddLog(ex.Message);
            }
        }
    }
}
