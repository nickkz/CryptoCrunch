using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace CCTriArb
{
    public class CKuCoin : CExchange
    {
        public CKuCoin() : base()
        {
            BaseURL = "https://api.kucoin.com/v1/open/tick";
            Name = "KuCoin";
            getAccounts();
        }

        public override void getAccounts()
        {
            //throw new NotImplementedException();
        }

        private HttpRequestMessage KuCoinPrivate(String endpoint, Dictionary<string, string> parameters, HttpMethod method)
        {
            try
            {
                Uri baseAddress;
                switch (server.serverType)
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

                // create APi Sign
                String ApiForSign = endpoint + "/" + nonce + "/" + strQuery;
                String Base64ForSign = CHelper.Base64Encode(ApiForSign);

                // bytestring and hashing code
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

                // Add a new Request Message
                HttpRequestMessage requestMessage = new HttpRequestMessage(method, baseAddress + endpoint + (strQuery.Length > 0 ? "?" + strQuery : "" ));

                // Add our custom headers
                requestMessage.Headers.Add("KC-API-SIGNATURE", signatureResult);
                requestMessage.Headers.Add("KC-API-KEY", API_KEY);
                requestMessage.Headers.Add("KC-API-NONCE", nonce);

                return requestMessage;
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
            return null;
        }

        public override async void pollOrders(object source, ElapsedEventArgs e)
        {
            if (pollingOrders)
                return;
            else
                pollingOrders = true;

            try
            {
                HttpClient httpClient = new HttpClient();
                foreach (CProduct product in dctProducts.Values)
                {
                    for (int active_dealt = 0; active_dealt < 2; active_dealt++)
                    {
                        String endpoint;
                        Dictionary<string, string> parameters;
                        if (active_dealt == 1)
                        {
                            endpoint = "/v1/order/dealt";
                            parameters = new Dictionary<string, string> {
                                { "symbol", product.Symbol }
                            };
                        }
                        else
                        {
                            endpoint = "/v1/order/active";
                            parameters = new Dictionary<string, string> {
                                { "symbol", product.Symbol }
                            };
                        }
                        HttpRequestMessage requestMessage = KuCoinPrivate(endpoint, parameters, HttpMethod.Get);

                        // Send the request to the server
                        HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                        // Just as an example I'm turning the response into a string here
                        string json = await response.Content.ReadAsStringAsync();

                        dynamic orderData = JsonConvert.DeserializeObject(json);
                        var orders = orderData.data;
                        if (orders != null)
                        {
                            if (active_dealt == 1)
                            {
                                var orderDealtAll = orders.datas;
                                foreach (var orderDealt in orderDealtAll)
                                {
                                    String oid = orderDealt.oid;
                                    String orderOid = orderDealt.orderOid;
                                    Double dealPrice = orderDealt.dealPrice;
                                    Double fee = orderDealt.fee;
                                    Double feeRate = orderDealt.feeRate;
                                    Double amount = orderDealt.amount;
                                    Double dealValue = orderDealt.dealValue;
                                    COrder order = null;
                                    if (server.dctIdToOrder.ContainsKey(oid))
                                        order = server.dctIdToOrder[oid];
                                    else if (server.dctIdToOrder.ContainsKey(orderOid))
                                        order = server.dctIdToOrder[orderOid];

                                    /*
                                    if (order == null)
                                    {
                                        order = new COrder(orderOid);
                                        //Server.colOrders.Add(order);
                                        Server.dctIdToOrder.Add(orderOid, order);
                                    }
                                    */

                                    if (order != null)
                                    {
                                        order.OID = oid;
                                        order.DealPrice = dealPrice;
                                        order.Fee = fee;
                                        order.FeeRate = feeRate;
                                        order.DealValue = dealValue;
                                        order.Filled = amount;
                                        order.Status = "Filled";
                                        order.TimeStampFilled = DateTime.Now;
                                        order.updateGUI();
                                    }
                                }
                            }
                            else
                            {
                                foreach (var orderSideSet in orders)
                                {
                                    String name = orderSideSet.Name;
                                    foreach (var orderSideSetOrders in orderSideSet)
                                    {
                                        foreach (var orderSideSetOrder in orderSideSetOrders)
                                        {
                                            int iAttrCount = 0;
                                            Double filled = 0;
                                            Double timeStamp = 0;
                                            String orderID = null;
                                            foreach (var orderSideSetOrderAttr in orderSideSetOrder)
                                            {
                                                // timestamp, side, price, size, executed, orderID
                                                switch (++iAttrCount)
                                                {
                                                    case 1:
                                                        timeStamp = orderSideSetOrderAttr;
                                                        break;

                                                    case 5:
                                                        filled = orderSideSetOrderAttr;
                                                        break;

                                                    case 6:
                                                        orderID = orderSideSetOrderAttr;
                                                        break;
                                                }
                                            }

                                            if (orderID != null && server.dctIdToOrder.ContainsKey(orderID))
                                            {
                                                COrder order = server.dctIdToOrder[orderID];
                                                if (filled > 0)
                                                {
                                                    order.Filled = filled;
                                                    order.TimeStampFilled = DateTime.Now;
                                                    if (order.Filled < order.Size)
                                                        order.Status = "Partial";
                                                }
                                                order.Status = "Queued";
                                                order.TimeStampSent = CHelper.ConvertFromUnixTimestamp(timeStamp);
                                                order.TimeStampLastUpdate = DateTime.Now;
                                                order.updateGUI();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
            pollingOrders = false;
        }

        public override async void pollPositions(object source, ElapsedEventArgs e)
        {
            if (pollingPositions)
                return;
            else
                pollingPositions = true;
            HttpClient httpClient = new HttpClient();
            try
            {
                String endpoint;
                Dictionary<string, string> parameters;
                endpoint = "/v1/account/balances";
                int totalPage = 99;
                int currentPage = 0;
                int limit = 12;
                while (++currentPage <= totalPage)
                {
                    parameters = new Dictionary<string, string> {
                        { "limit", limit.ToString() },
                        { "page", currentPage.ToString() },
                    };
                    HttpRequestMessage requestMessage = KuCoinPrivate(endpoint, parameters, HttpMethod.Get);

                    // Send the request to the server
                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                    // Just as an example I'm turning the response into a string here
                    string json = await response.Content.ReadAsStringAsync();

                    dynamic balanceData = JsonConvert.DeserializeObject(json);
                    var numTicks = balanceData.timestamp;

                    var posixTime = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc);
                    //var time = posixTime.AddMilliseconds(numTicks);

                    var balances = balanceData.data;
                    if (balances != null)
                    {
                        var total = balances.total;
                        var pageNos = balances.pageNos;
                        int.TryParse(pageNos.ToString(), out totalPage);
                        foreach (var pos in balances.datas)
                        {
                            var coinType = pos.coinType;
                            var balance = pos.balance;
                            if (coinType.ToString().Contains("USD"))
                                server.AddLog("Found " + coinType + "!");
                            String symbol = (coinType.ToString().Equals("USDT")) ? coinType : coinType + "-USDT";
                            if (dctProducts.ContainsKey(symbol))
                            {
                                CProduct product = dctProducts[symbol];
                                product.TimeStampLastBalance = DateTime.Now;
                                Double dbal = 0;
                                Double.TryParse(balance.ToString(), out dbal);
                                product.SetBalance(dbal);
                                product.updateGUI();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
            pollingPositions = false;
        }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            if (pollingTicks)
                return;
            else
                pollingTicks = true;
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
                    Decimal last;
                    Decimal.TryParse(tickData.data.lastDealPrice.ToString(), out last);
                    product.SetLast(last);
                    product.Volume = tickData.data.volValue;

                    long numTicks = tickData.data.datetime;
                    var posixTime = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc);
                    var time = posixTime.AddMilliseconds(numTicks);
                    product.TimeStampLastTick = time;

                    foreach (CStrategy strategy in product.colStrategy)
                    {
                        strategy.updateGUI();
                    }
                }
                catch (Exception ex)
                {
                    server.AddLog(ex.Message);
                }
            }
            pollingTicks = false;
        }

        public override async void trade(CStrategy strategy, int? leg, OrderSide? side, CProduct product, Double size, Double price)
        {
            try
            {
                String endpoint = "/v1/order";
                Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "amount", size.ToString() },
                    { "price", price.ToString() },
                    { "symbol", product.Symbol },
                    { "type", side.GetValueOrDefault().ToString().ToUpper() }
                };
                HttpRequestMessage requestMessage = KuCoinPrivate(endpoint, parameters, HttpMethod.Post);

                // Create a client
                HttpClient httpClient = new HttpClient();

                // Send the request to the server
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                // get json back
                string json = await response.Content.ReadAsStringAsync();

                // parse order String
                dynamic orderData = JsonConvert.DeserializeObject(json);
                String orderID;
                try
                {
                    orderID = orderData.data.orderOid;
                }
                catch (Exception ex)
                {
                    server.AddLog(ex.Message);
                    orderID = "";
                }
                if (!orderID.Equals(""))
                {
                    COrder order = new COrder(orderID);
                    order.Product = product;
                    order.Side = side.GetValueOrDefault();
                    order.Size = size;
                    order.Price = price;
                    String orderStatus = orderData.msg.ToString();
                    if (orderStatus.Equals("OK") || orderStatus.Equals("Sent"))
                        order.Status = "Sent";
                    else
                        order.Status = "Unknown";

                    order.Strategy = strategy;
                    order.Exchange = this;
                    Double timeStamp = orderData.timestamp;
                    order.TimeStampSent = CHelper.ConvertFromUnixTimestamp(timeStamp);

                    server.AddLog("Created Order " + this.Name + " " + orderID + " " + product + " " + side + " " + size + " " + price);

                    // add order to global Orders
                    server.colOrders.Add(order);
                    server.dctIdToOrder[orderID] = order;

                    // add order to strategy orders
                    strategy.DctOrders[orderID] = order;
                    if (leg != null)
                        strategy.DctLegToOrder[(int)leg] = order;

                    // cleanup
                    order.updateGUI();
                    server.AddLog(json);
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
        }

        public override async void cancel(string orderID)
        {
            try
            {
                String endpoint = "/v1/cancel-order";  // API endpoint
                COrder order = server.dctIdToOrder[orderID];
                CProduct product = order.Product;
                Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "orderOid", orderID },
                    { "symbol", product.Symbol },
                    { "type", order.Side.ToString().ToUpper() }
                };
                HttpRequestMessage requestMessage = KuCoinPrivate(endpoint, parameters, HttpMethod.Post);
                HttpClient httpClient = new HttpClient();

                // Send the request to the server
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                // get back cancel message
                string json = await response.Content.ReadAsStringAsync();

                // parse order String
                dynamic cancelorderData = JsonConvert.DeserializeObject(json);
                var orders = cancelorderData.data;

                order.Strategy.State = CStrategy.StrategyState.Inactive;
                order.Status = "Cancelled";
                order.TimeStampLastUpdate = DateTime.Now;
                order.updateGUI();
                server.AddLog(json);
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
        }

        public override async void cancelAll()
        {
            try
            {
                String endpoint = "/v1/order/cancel-all";  // API endpoint
                HttpClient httpClient = new HttpClient();
                foreach (CProduct product in server.dctServerProducts.Values)
                {
                    if (product.Exchange.Equals(this))
                    {
                        Dictionary<string, string> parameters = new Dictionary<string, string> {
                            { "symbol", product.Symbol }
                        };

                        HttpRequestMessage requestMessage = KuCoinPrivate(endpoint, parameters, HttpMethod.Post);

                        // Send the request to the server
                        HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                        // get back message
                        string json = await response.Content.ReadAsStringAsync();

                        // parse order String
                        dynamic cancelorderData = JsonConvert.DeserializeObject(json);
                        var orders = cancelorderData.data;

                        foreach (COrder order in server.colOrders)
                        {
                            if (order.Product.Equals(product))
                            {
                                if (!order.Status.Equals("Cancelled"))
                                {
                                    order.Status = "Cancelled";
                                    order.TimeStampLastUpdate = DateTime.Now;
                                    order.updateGUI();
                                }
                            }
                        }
                        server.AddLog(json);
                    }
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
        }
    }
}
