using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ExchangeSharp;
using Newtonsoft.Json;

namespace CCTriArb
{
    public abstract class CESExchange : CExchange
    {
        protected ExchangeAPI api;

        public CESExchange() : base() { }

        public override void getAccounts() { }

        public override void pollTicks(object source, ElapsedEventArgs e)
        {
            lock (_pollTicksLock)
            {
                foreach (CProduct product in dctExchangeProducts.Values)
                {
                    try
                    {
                        if (product.Symbol.Length > 4)
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
                for (bool open_completed = false; open_completed; open_completed = !open_completed)
                {
                    IEnumerable<ExchangeOrderResult> resultOrders;
                    if (open_completed)
                        resultOrders = await api.GetCompletedOrderDetailsAsync();
                    else
                        resultOrders = await api.GetOpenOrderDetailsAsync();

                    foreach (ExchangeOrderResult orderOpen in resultOrders)
                    {
                        String orderID = orderOpen.OrderId;
                        Decimal amount = orderOpen.Amount;
                        Decimal amountFilled = orderOpen.AmountFilled;
                        Decimal averagePrice = orderOpen.AveragePrice;
                        Boolean isBuy = orderOpen.IsBuy;
                        DateTime orderDate = orderOpen.OrderDate;
                        ExchangeAPIOrderResult result = orderOpen.Result;
                        String symbol = orderOpen.Symbol;
                        COrder order = null;
                        if (server.dctIdToOrder.ContainsKey(orderID))
                            order = server.dctIdToOrder[orderID];

                        if (order != null)
                        {
                            order.OrderID = orderID;
                            order.DealPrice = (Double)averagePrice;
                            //order.Fee = fee;
                            //order.FeeRate = feeRate;
                            order.Size = (double)amount;
                            order.Filled = (Double)amountFilled;
                            switch (result)
                            {
                                case ExchangeAPIOrderResult.Canceled:
                                    order.Status = COrder.OrderState.Cancelled;
                                    break;

                                case ExchangeAPIOrderResult.Error:
                                    order.Status = COrder.OrderState.Error;
                                    break;

                                case ExchangeAPIOrderResult.Filled:
                                    order.Status = COrder.OrderState.Filled;
                                    break;

                                case ExchangeAPIOrderResult.FilledPartially:
                                    order.Status = COrder.OrderState.Partial;
                                    break;

                                case ExchangeAPIOrderResult.Pending:
                                    order.Status = COrder.OrderState.Queued;
                                    break;

                                case ExchangeAPIOrderResult.Unknown:
                                    order.Status = COrder.OrderState.Unknown;
                                    break;
                            }
                            order.TimeStampFilled = orderDate;
                            order.updateGUI();
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
            try
            {
                Dictionary<String, Decimal> positions = await api.GetAmountsAsync();
                foreach (var pos in positions)
                {
                    var coinType = pos.Key;
                    var balance = pos.Value;
                    String symbol = exchangeUSD(coinType.ToString());
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
            } 
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
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
                    order.Status = COrder.OrderState.Sent;
                else
                    order.Status = COrder.OrderState.Unknown;

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
                if (server.dctIdToOrder.ContainsKey(orderID))
                {
                    COrder order = server.dctIdToOrder[orderID];
                    if (order.canCancel())
                    {
                        await api.CancelOrderAsync(orderID);
                        System.Threading.Thread.Sleep(100);
                        order.Status = COrder.OrderState.Cancelled;
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
                var processOrders = server.colServerOrders.Where(i => i.Exchange.Equals(this) && i.canCancel());
                foreach (COrder order in processOrders)
                {
                    cancel(order.OrderID);
                    order.Status = COrder.OrderState.Cancelled;
                    order.TimeStampLastUpdate = DateTime.Now;
                    order.Strategy.State = CStrategy.StrategyState.Inactive;
                    order.updateGUI();
                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
        }
    }
}
