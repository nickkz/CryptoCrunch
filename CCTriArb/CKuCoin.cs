using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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

        public override async void pollOrders(object source, ElapsedEventArgs e)
        {
            try
            {
                ServerType serverType = Server.serverType;
                CProduct product = dctProducts["BTC-USDT"];
                Uri baseAddress;
                switch (serverType)
                {
                    case ServerType.Debugging:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-proxy.com");
                        break;

                    case ServerType.Mock:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-mock.com");
                        break;

                    default:
                        baseAddress = new Uri("https://api.kucoin.com");
                        break;
                }

                String API_KEY = Properties.Settings.Default.KUCOIN_API_KEY;
                String API_SECRET = Properties.Settings.Default.KUCOIN_API_SECRET;
                String endpoint = "/v1/order/active";  // API endpoint

                HttpClient httpClient = new HttpClient();

                Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "symbol", product.Symbol }
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
                String ApiForSign = endpoint + "/" + nonce + "/" + strQuery;
                String Base64ForSign = CHelper.Base64Encode(ApiForSign);

                UTF8Encoding encoding = new System.Text.UTF8Encoding();
                byte[] keyByte = encoding.GetBytes(API_SECRET);
                byte[] messageBytes = encoding.GetBytes(Base64ForSign);
                HMACSHA256 hmacsha256 = new HMACSHA256(keyByte);
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);

                byte[] ba = hashmessage;
                StringBuilder hex = new StringBuilder(ba.Length * 2);
                foreach (byte b in ba)
                    hex.AppendFormat("{0:x2}", b);

                String signatureResult = hex.ToString();

                // Create a client
                httpClient = new HttpClient();

                // Add a new Request Message
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, baseAddress + endpoint + "?" + strQuery);

                // Add our custom headers
                requestMessage.Headers.Add("KC-API-SIGNATURE", signatureResult);
                requestMessage.Headers.Add("KC-API-KEY", API_KEY);
                requestMessage.Headers.Add("KC-API-NONCE", nonce);

                // Send the request to the server
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                // Just as an example I'm turning the response into a string here
                string json = await response.Content.ReadAsStringAsync();

                dynamic orderData = JsonConvert.DeserializeObject(json);
                var orders = orderData.data;
                if (orders != null )
                {
                    foreach (var orderSideSet in orders)
                    {
                        String name = orderSideSet.Name;
                        foreach (var orderSideSetOrders in orderSideSet)
                        {
                            foreach (var orderSideSetOrder in orderSideSetOrders)
                            {
                                Server.AddLog(orderSideSetOrder + " Found");
                                int iAttrCount = 0;
                                Double executed = 0;
                                String orderID = null;
                                foreach (var orderSideSetOrderAttr in orderSideSetOrder)
                                {
                                    // timestamp, side, price, size, executed, orderID
                                    switch (++iAttrCount)
                                    {
                                        case 5:
                                            executed = orderSideSetOrderAttr;
                                            break;

                                        case 6:
                                            orderID = orderSideSetOrderAttr;
                                            break;
                                    }
                                }

                                if (orderID != null && Server.dctIdToOrder.ContainsKey(orderID))
                                {
                                    COrder order = Server.dctIdToOrder[orderID];
                                    order.Executed = executed;
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Server.AddLog(ex.Message);
            }
        }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            var wc = new WebClient();
            wc.Headers.Add("user-agent", USER_AGENT);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            foreach (CProduct product in dctProducts.Values)
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

        public override async void trade(CStrategy strategy, ServerType serverType, OrderSide? side, CProduct product, Double size, decimal? price)
        {
            try
            {
                Uri baseAddress;
                switch (serverType)
                {
                    case ServerType.Debugging:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-proxy.com");
                        break;

                    case ServerType.Mock:
                        baseAddress = new Uri("https://private-f6a2b2-kucoinapidocs.apiary-mock.com");
                        break;

                    default:
                        baseAddress = new Uri("https://api.kucoin.com");
                        break;
                }

                String API_KEY = Properties.Settings.Default.KUCOIN_API_KEY;
                String API_SECRET = Properties.Settings.Default.KUCOIN_API_SECRET;
                String endpoint = "/v1/order";  // API endpoint

                HttpClient httpClient = new HttpClient();
                
                Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "amount", size.ToString() },
                    { "price", price.ToString() },
                    { "symbol", product.Symbol },
                    { "type", side.GetValueOrDefault().ToString().ToUpper() }
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

                String ApiForSign = endpoint + "/" + nonce + "/" + strQuery;
                String Base64ForSign = CHelper.Base64Encode(ApiForSign);

                UTF8Encoding encoding = new System.Text.UTF8Encoding();
                byte[] keyByte = encoding.GetBytes(API_SECRET);
                byte[] messageBytes = encoding.GetBytes(Base64ForSign);
                HMACSHA256 hmacsha256 = new HMACSHA256(keyByte);
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);

                byte[] ba = hashmessage;
                StringBuilder hex = new StringBuilder(ba.Length * 2);
                foreach (byte b in ba)
                    hex.AppendFormat("{0:x2}", b);

                String signatureResult = hex.ToString();

                // Create a client
                httpClient = new HttpClient();

                // Add a new Request Message
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, baseAddress + endpoint + "?" + strQuery);

                // Add our custom headers
                requestMessage.Headers.Add("KC-API-SIGNATURE", signatureResult);
                requestMessage.Headers.Add("KC-API-KEY", API_KEY);
                requestMessage.Headers.Add("KC-API-NONCE", nonce);

                // Send the request to the server
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                string json = await response.Content.ReadAsStringAsync();

                // parse order String
                dynamic orderData = JsonConvert.DeserializeObject(json);
                String orderID = orderData.data.orderOid;
                COrder order = new COrder(orderID);
                order.Product = product;
                order.Side = side.GetValueOrDefault();
                order.Size = size;
                order.Status = orderData.msg;
                order.Strategy = strategy;
                order.Exchange = this;
                Double timeStamp = orderData.timestamp;
                //order.TimeStamp = CHelper.ConvertFromUnixTimestamp(timeStamp);

                // add order to both Strategy orders and global Orders
                Server.colOrders.Add(order);
                Server.dctIdToOrder.Add(orderID, order);
                strategy.DctOrders.Add(orderID, order);

                Server.AddLog(json);
            }
            catch (Exception ex)
            {
                Server.AddLog(ex.Message);
            }
        }
    }
}
