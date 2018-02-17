using ExchangeSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CCTriArb
{
    class CBinance : CExchange
    {
        ExchangeBinanceAPI api;

        public CBinance(CStrategyServer server) : base(server)
        {
            api = new ExchangeBinanceAPI();
            BaseURL = "https://api.kucoin.com/v1/open/tick";
            Name = api.Name;
            getAccounts();
        }

        public override void cancel(string orderID)
        {
            throw new NotImplementedException();
        }

        public override void getAccounts()
        {
            //throw new NotImplementedException();
        }

        public override async void pollOrders(object source, ElapsedEventArgs e)
        {
            if (pollingOrders)
                return;
            else
                pollingOrders = true;

            try
            {
                String API_KEY = Properties.Settings.Default.KUCOIN_API_KEY;
                String API_SECRET = Properties.Settings.Default.KUCOIN_API_SECRET;
                ServerType serverType = Server.serverType;
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
                                { "symbol", product.Symbol }//,
                                //{ "limit", "20" }
                            };
                        }
                        else
                        {
                            endpoint = "/v1/order/active";
                            parameters = new Dictionary<string, string> {
                                { "symbol", product.Symbol }
                            };
                        }
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
                                    if (Server.dctIdToOrder.ContainsKey(oid))
                                        order = Server.dctIdToOrder[oid];
                                    else if (Server.dctIdToOrder.ContainsKey(orderOid))
                                        order = Server.dctIdToOrder[orderOid];

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

                                            if (orderID != null && Server.dctIdToOrder.ContainsKey(orderID))
                                            {
                                                COrder order = Server.dctIdToOrder[orderID];
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
                Server.AddLog(ex.Message);
            }
            pollingOrders = false;
        }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            if (pollingTicks)
                return;
            else
                pollingTicks = true;
            foreach (CProduct product in dctProducts.Values)
            {
                try
                {
                    ExchangeTicker ticker = api.GetTicker(product.Symbol);
                    //Console.WriteLine("On the Binance exchange, 1 " + product.Symbol + " is worth {0} USD.", ticker.Bid);

                    product.Bid = ticker.Bid;
                    product.Ask = ticker.Ask;
                    product.Last = ticker.Last;
                    product.Volume = ticker.Volume.QuantityAmount;
                    DateTime DtUpdate = ticker.Volume.Timestamp;

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
            pollingTicks = false;
        }

        public override void trade(CStrategy strategy, OrderSide? side, CProduct product, Double size, Double price)
        {
            try
            {
                String API_KEY = Properties.Settings.Default.BINANCE_API_KEY;
                String API_SECRET = Properties.Settings.Default.BINANCE_API_SECRET;

                api.LoadAPIKeysUnsecure(API_KEY, API_SECRET);
                Server.AddLog("Setting up " + side + " " + product.Symbol + " Trade on " + api.Name);

                ExchangeTicker ticker = api.GetTicker(product.Symbol);
                ExchangeOrderResult result = api.PlaceOrder(new ExchangeOrderRequest
                {
                    Amount = (Decimal) size,
                    IsBuy = (side == OrderSide.Buy),
                    Price = (Decimal) price,
                    Symbol = product.Symbol
                });

                System.Threading.Thread.Sleep(100);

                String orderID = result.OrderId;
                Server.AddLog("Calling Order OrderID: " + result.OrderId + " Date: " + result.OrderDate + " Result: " + result.Result);
                //result = api.GetOrderDetails(result.OrderId);
                //Server.AddLog("After calling GetOrderDetails OrderID: " + result.OrderId + " Date: " + result.OrderDate + " Result: " + result.Result);
                //Server.AddLog("Placed an order on " + Name + " for 0.0015 bitcoin at {0} USD. Status is {1}. Order id is {2}." + ticker.Ask + result.Result + result.OrderId);

                COrder order = new COrder(orderID);
                order.Product = product;
                order.Side = side.GetValueOrDefault();
                order.Size = size;
                order.Price = price;
                String orderStatus = result.Result.ToString();
                if (orderStatus.Equals("OK") || orderStatus.Equals("Sent"))
                    order.Status = "Sent";
                else
                    order.Status = result.Result.ToString();

                order.Strategy = strategy;
                order.Exchange = this;
                order.TimeStampSent = result.OrderDate;

                // add order to both Strategy orders and global Orders
                Server.colOrders.Add(order);
                Server.dctIdToOrder.Add(orderID, order);
                strategy.DctOrders.Add(orderID, order);
                order.updateGUI();
            }
            catch (Exception ex)
            {
                Server.AddLog(ex.Message);
            }
        }

        public override async void cancelAll()
        {
            try
            {
                Uri baseAddress;
                switch (Server.serverType)
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
                String endpoint = "/v1/order/cancel-all";  // API endpoint

                HttpClient httpClient = new HttpClient();

                foreach (CProduct product in Server.dctProducts.Values)
                {
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
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, baseAddress + endpoint + "?" + strQuery);

                    // Add our custom headers
                    requestMessage.Headers.Add("KC-API-SIGNATURE", signatureResult);
                    requestMessage.Headers.Add("KC-API-KEY", API_KEY);
                    requestMessage.Headers.Add("KC-API-NONCE", nonce);

                    // Send the request to the server
                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);

                    string json = await response.Content.ReadAsStringAsync();

                    // parse order String
                    dynamic cancelorderData = JsonConvert.DeserializeObject(json);
                    var orders = cancelorderData.data;

                    foreach (COrder order in Server.colOrders)
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
                    Server.AddLog(json);
                }
            }
            catch (Exception ex)
            {
                Server.AddLog(ex.Message);
            }
        }

    }
}
