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
        static object _pollTicksLock = new object();
        static object _pollOrdersLock = new object();

        public CBinance() : base()
        {
            api = new ExchangeBinanceAPI();
            BaseURL = "https://api.kucoin.com/v1/open/tick";
            Name = api.Name;
            getAccounts();
            api.LoadAPIKeysUnsecure(Properties.Settings.Default.BINANCE_API_KEY, Properties.Settings.Default.BINANCE_API_SECRET);
        }

        public override void getAccounts()
        {
        }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            lock (_pollTicksLock)
            {
                foreach (CProduct product in dctExchangeProducts.Values)
                {
                    try
                    {
                        ExchangeTicker ticker = api.GetTicker(product.Symbol);
                        product.Bid = ticker.Bid;
                        product.Ask = ticker.Ask;
                        product.Last = ticker.Last;
                        Decimal last;
                        Decimal.TryParse(ticker.Last.ToString(), out last);
                        product.SetLast(last);
                        product.Volume = ticker.Volume.QuantityAmount;
                        product.TimeStampLastTick = ticker.Volume.Timestamp;
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
            }
        }

        public override async void pollOrders(object source, ElapsedEventArgs e)
        {
            if (pollingOrders)
                return;
            else
                pollingOrders = true;
            try
            {
                IEnumerable<ExchangeOrderResult> resultOpenOrders = await api.GetOpenOrderDetailsAsync();
                IEnumerable<ExchangeOrderResult> resultCompletedOrders = await api.GetCompletedOrderDetailsAsync();
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
            Dictionary<String, Decimal> positions = await api.GetAmountsAsync();
            foreach (var pos in positions)
            {
                var coinType = pos.Key;
                var balance = pos.Value;
                String symbol = (coinType.ToString().Equals("USDT")) ? coinType : coinType + "USDT";
                if (dctExchangeProducts.ContainsKey(symbol))
                {
                    CProduct product = dctExchangeProducts[symbol];
                    product.TimeStampLastBalance = DateTime.Now;
                    Double dbal = 0;
                    Double.TryParse(balance.ToString(), out dbal);
                    product.SetBalance(dbal);
                    product.updateGUI();
                }
            }
            pollingPositions = false;
        }

        public override void trade(CStrategy strategy, int? leg, OrderSide? side, CProduct product, Double size, Double price)
        {
            try
            {
                server.AddLog("Init " + side + " " + product.Symbol + " Trade on " + api.Name);

                ExchangeTicker ticker = api.GetTicker(product.Symbol);
                ExchangeOrderResult result = api.PlaceOrder(new ExchangeOrderRequest
                {
                    Amount = (Decimal)size,
                    IsBuy = (side == OrderSide.Buy),
                    Price = (Decimal)price,
                    Symbol = product.Symbol
                });

                System.Threading.Thread.Sleep(100);

                String orderID = result.OrderId;
                server.AddLog("Calling Order OrderID: " + result.OrderId + " Date: " + result.OrderDate + " Result: " + result.Result);

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

                server.AddLog("Created Order " + this.Name + " " + orderID + " " + product + " " + side + " " + size + " " + price);

                // add order to global Orders
                server.colServerOrders.Add(order);
                server.dctIdToOrder[orderID] = order;

                // add order to strategy orders
                strategy.DctStrategyOrders[orderID] = order;
                if (leg != null)
                    strategy.DctLegToOrder[(int)leg] = order;

                // cleanup
                order.updateGUI();
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
                if (server.dctIdToOrder.ContainsKey(orderID) )
                {
                    COrder order = server.dctIdToOrder[orderID];
                    if (!order.Status.Equals("Cancelled"))
                    {
                        await api.CancelOrderAsync(orderID);
                        System.Threading.Thread.Sleep(100);
                        order.Strategy.State = CStrategy.StrategyState.Inactive;
                        order.Status = "Cancelled";
                        order.TimeStampLastUpdate = DateTime.Now;
                        order.updateGUI();
                    }
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
        }

        public override void cancelAll()
        {
            try
            {
                foreach (COrder order in server.colServerOrders)
                {
                    if (order.Exchange == this && (!order.Status.Equals("Cancelled")))
                    {
                        cancel(order.OrderID);
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
