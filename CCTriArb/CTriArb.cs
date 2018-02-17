using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public class CTriArb : CStrategy
    {
        private const int MAKER_LEG = 2;
        public enum StrategyState
        {
            Inactive,
            Waiting,
            Maker,
            Taker
        }
        public StrategyState State { get; set; }

        internal CTriArb(CStrategyServer server, Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs) : base(server, dctLegs)
        {
            State = StrategyState.Inactive;
        }

        public CExchange Exchange
        {
            get
            {
                return dctLegs[1].Item2.Exchange;
            }
        }

        public Double? ProfitAAA {
            get {
                if (Leg1Ask == null || Leg2Ask == null || Leg3Bid == null)
                    return null;

                Double baseUSD = 100;
                Double leg1 = baseUSD / (Double)Leg1Ask;
                Double leg2 = GetLegSide(2) == OrderSide.Buy ? leg1 / (Double)Leg2Ask : leg1 * (Double)Leg2Bid;
                Double leg3 = leg2 * (Double)Leg3Bid;
                return leg3 - baseUSD;
            }
        }

        public Double? ProfitPAA
        {
            get
            {
                if (Leg1Bid == null || Leg2Ask == null || Leg3Bid == null)
                    return null;

                Double baseUSD = 100;
                Double leg1 = baseUSD / (Double)Leg1Bid;
                Double leg2 = GetLegSide(2) == OrderSide.Buy ? leg1 / (Double)Leg2Ask : leg1 * (Double)Leg2Bid;
                Double leg3 = leg2 * (Double)Leg3Bid;
                return leg3 - baseUSD;
            }
        }

        public Double? ProfitPPA
        {
            get
            {
                if (Leg1Bid == null || Leg2Bid == null || Leg3Bid == null)
                    return null;

                Double baseUSD = 100;
                Double leg1 = baseUSD / (Double)Leg1Bid;
                Double leg2 = GetLegSide(2) == OrderSide.Buy ? leg1 / (Double)Leg2Bid : leg1 * (Double)Leg2Ask;
                Double leg3 = leg2 * (Double)Leg3Bid;
                return leg3 - baseUSD;
            }
        }

        public Double? ProfitPPP
        {
            get
            {
                if (Leg1Bid == null || Leg2Bid == null || Leg3Bid == null)
                    return null;

                Double baseUSD = 100;
                Double leg1 = baseUSD / (Double)Leg1Bid;
                Double leg2 = GetLegSide(2) == OrderSide.Buy ? leg1 / (Double)Leg2Bid : leg1 * (Double)Leg2Ask;
                Double leg3 = leg2 * (Double)Leg3Ask;
                return leg3 - baseUSD;
            }
        }

        public Double? Profit
        {
            get
            {
                if (Leg1Bid == null || Leg2Ask == null || Leg3Bid == null)
                    return null;

                Double baseUSD = server.TradeUSD.GetValueOrDefault();
                if (baseUSD < 1)
                    baseUSD = 1;
                //for now just assume we trade leg 2 as maker

                // leg 1 is taker (active)
                Double leg1 = GetLegSide(1) == OrderSide.Buy ? baseUSD / (Double)Leg1Ask : baseUSD * (Double)Leg2Bid;

                // leg 2 is maker (passive)
                Double leg2 = GetLegSide(2) == OrderSide.Buy ? leg1 / (Double)Leg2Bid : leg1 * (Double)Leg2Ask;

                // leg 3 is taker (active)
                Double leg3 = GetLegSide(3) == OrderSide.Buy ? leg2 / (Double)Leg3Ask : leg2 * (Double)Leg3Bid;

                // return Profit
                return leg3 - baseUSD;
            }
        }

        public override void updateGUI()
        {
            /*
            this.OnPropertyChanged("ProfitAAA");
            this.OnPropertyChanged("ProfitPAA");
            this.OnPropertyChanged("ProfitPPA");
            this.OnPropertyChanged("ProfitPPP");
            */
            this.OnPropertyChanged("Profit");
            base.updateGUI();
        }

        internal void activateStrategy()
        {
            Double profitUSD = server.TradeUSD.GetValueOrDefault() * server.MinProfit.GetValueOrDefault();
            if (Profit >= profitUSD)
                State = StrategyState.Maker;
            else
                State = StrategyState.Waiting;
            this.OnPropertyChanged("State");
            base.updateGUI();
        }

        public override void cycleStrategy()
        {
            if (processStrategy)
                return;
            else
                processStrategy = true;

            try
            {
                Double profitUSD = server.TradeUSD.GetValueOrDefault() * server.MinProfit.GetValueOrDefault();
                if (State == StrategyState.Inactive)
                    return;

                if (State == StrategyState.Waiting)
                {
                    if (Profit >= profitUSD)
                    {
                        State = StrategyState.Maker;
                        this.OnPropertyChanged("State");
                        base.updateGUI();
                    }
                    else
                    {
                        processStrategy = false;
                        return;
                    }
                }

                if (State == StrategyState.Maker)
                {
                    // check for filled order, if so go to Taker State
                    if (DctLegToOrder.ContainsKey(MAKER_LEG))
                    {
                        COrder order = DctLegToOrder[MAKER_LEG];
                        if (order.Status == "Filled")
                        {
                            State = StrategyState.Taker;
                            this.OnPropertyChanged("State");
                            base.updateGUI();
                            processStrategy = false;
                            return;
                        }
                        else if (Profit < profitUSD)
                        {
                            order.cancel();
                            State = StrategyState.Waiting;
                            this.OnPropertyChanged("State");
                            base.updateGUI();
                            processStrategy = false;
                            return;
                        }
                        // check if not on edge of market, cancel and create another order
                    }

                    // create new order
                    double dUSD = server.TradeUSD.GetValueOrDefault();
                    OrderSide side = dctLegs[MAKER_LEG].Item1;
                    CProduct product = dctLegs[MAKER_LEG].Item2;
                    Double size = 0.00001;
                    Double price;

                    if (product.Symbol.EndsWith("USD") || product.Symbol.EndsWith("USDT"))
                    {
                        size = dUSD / (double)product.Last;
                    }
                    else if (product.Symbol.StartsWith("USD") || product.Symbol.StartsWith("USDT"))
                    {
                        if (side == OrderSide.Buy)
                        {
                            size = dUSD;
                        }
                        else
                        {
                            size = dUSD / (double)product.Last;
                        }
                    }
                    else
                    {
                        String productUSD = product.Symbol.Substring(0, 3);
                        if (product.Exchange is CKuCoin)
                            productUSD += "-";
                        productUSD += "USDT";
                        CProduct productExchange = DctProducts[productUSD];
                        if (productExchange.Symbol.Equals(productUSD))
                        {
                            size = dUSD / (double)productExchange.Last;
                        }
                    }

                    price = (double)(side == OrderSide.Buy ? product.Bid : product.Ask);
                    dctLegs[MAKER_LEG].Item2.Exchange.trade(this, side, product, Math.Round(size, product.PrecisionSize), Math.Round(price, product.PrecisionPrice));

                }

                if (State == StrategyState.Taker)
                {
                    // check for filled order, if so go to waiting
                    // check for orders not at mid, if so cancel and replace
                    double dUSD = server.TradeUSD.GetValueOrDefault();
                    OrderSide side = dctLegs[currentLeg].Item1;
                    CProduct product = dctLegs[currentLeg].Item2;
                    Double size = 0.00001;
                    Double price;

                    if (product.Symbol.EndsWith("USD") || product.Symbol.EndsWith("USDT"))
                    {
                        size = dUSD / (double)product.Last;
                    }
                    else if (product.Symbol.StartsWith("USD") || product.Symbol.StartsWith("USDT"))
                    {
                        if (side == OrderSide.Buy)
                        {
                            size = dUSD;
                        }
                        else
                        {
                            size = dUSD / (double)product.Last;
                        }
                    }
                    else
                    {
                        String productUSD = product.Symbol.Substring(0, 3);
                        if (product.Exchange is CKuCoin)
                            productUSD += "-";
                        productUSD += "USDT";
                        CProduct productExchange = DctProducts[productUSD];
                        if (productExchange.Symbol.Equals(productUSD))
                        {
                            size = dUSD / (double)productExchange.Last;
                        }
                    }

                    price = ((Double)product.Bid + (Double)product.Ask) / 2.0;
                    dctLegs[currentLeg].Item2.Exchange.trade(this, side, product, Math.Round(size, product.PrecisionSize), Math.Round(price, product.PrecisionPrice));
                    if (++currentLeg > dctLegs.Count)
                        currentLeg = 1;

                }
            }
            catch (Exception ex)
            {
                server.AddLog(ex.Message);
            }
            processStrategy = false;
        }

        public void tradeNext(ServerType serverType, Double dUSD, Boolean active)
        {
            OrderSide side = dctLegs[currentLeg].Item1;
            CProduct product = dctLegs[currentLeg].Item2;
            Double size = 0.00001;
            Double price;

            if (product.Symbol.EndsWith("USD") || product.Symbol.EndsWith("USDT"))
            {
                size = dUSD / (double)product.Last;
            }
            else if (product.Symbol.StartsWith("USD") || product.Symbol.StartsWith("USDT"))
            {
                if (side == OrderSide.Buy)
                {
                    size = dUSD;
                }
                else
                {
                    size = dUSD / (double)product.Last;
                }
            }
            else
            {
                String productUSD = product.Symbol.Substring(0, 3);
                if (product.Exchange is CKuCoin)
                    productUSD += "-";
                productUSD += "USDT";
                CProduct productExchange = DctProducts[productUSD];
                if (productExchange.Symbol.Equals(productUSD))
                {
                    size = dUSD / (double)productExchange.Last;
                }
            }

            if (active)
            {
                price = ((Double)product.Bid + (Double)product.Ask) / 2.0;
            }
            else if (side == OrderSide.Buy)
            {
                price = (Double) product.Bid;
            }
            else
            {
                price = (Double)product.Ask;
            }
            dctLegs[currentLeg].Item2.Exchange.trade(this, side, product, Math.Round(size, product.PrecisionSize), Math.Round(price, product.PrecisionPrice));
            if (++currentLeg > dctLegs.Count)
                currentLeg = 1;
        }

        public override string ToString()
        {
            return Leg1 + " "  + Leg2 + " " + Leg3;
        }

    }
}
