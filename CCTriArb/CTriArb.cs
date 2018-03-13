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
        private int MakerLeg { get; set; }

        internal CTriArb(Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs, int makerLeg) : base(dctLegs)
        {
            State = StrategyState.Inactive;
            MakerLeg = makerLeg;
            Continuous = false;
        }

        public CExchange Exchange
        {
            get
            {
                return dctLegs[1].Item2.Exchange;
            }
        }

        public Boolean Continuous { get; set; }

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
            this.OnPropertyChanged("Profit");
            this.OnPropertyChanged("State");
            base.updateGUI();
        }

        internal void activateStrategy(Boolean continuous)
        {
            Double profitUSD = server.TradeUSD.GetValueOrDefault() * server.MinProfit.GetValueOrDefault();
            Continuous = continuous;
            if (Continuous)
                State = StrategyState.Continuous;
            else
                State = StrategyState.Active;
        }

        internal Double GetSize(OrderSide side, CProduct product)
        {
            // create new order
            double dUSD = server.TradeUSD.GetValueOrDefault();
            Double size = 0.00001;

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
                CProduct productExchange = DctStrategyProducts[productUSD];
                if (productExchange.Symbol.Equals(productUSD))
                {
                    size = dUSD / (double)productExchange.Last;
                    if (product.MinSize.HasValue)
                        size = Math.Max(size, product.MinSize.GetValueOrDefault());
                }
            }
            return size;
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
                switch (State)
                {
                    case StrategyState.Inactive:
                        break;

                    case StrategyState.Active:
                    case StrategyState.Continuous:
                        if (Profit >= profitUSD)
                        {
                            State = StrategyState.MakerSend;
                            DctLegToOrder.Clear();
                            goto case StrategyState.MakerSend;
                        }
                        break;

                    case StrategyState.MakerSend:
                        CurrentLeg = MakerLeg;
                        double dUSD = server.TradeUSD.GetValueOrDefault();
                        OrderSide sideMaker = dctLegs[CurrentLeg].Item1;
                        CProduct productMaker = dctLegs[CurrentLeg].Item2;
                        Double sizeMaker = GetSize(sideMaker, productMaker);
                        Double priceMaker = (double)(sideMaker == OrderSide.Buy ? productMaker.Bid : productMaker.Ask);
                        dctLegs[MakerLeg].Item2.Exchange.trade(this, MakerLeg, sideMaker, productMaker, Math.Round(sizeMaker, productMaker.PrecisionSize), Math.Round(priceMaker, productMaker.PrecisionPrice));
                        State = StrategyState.MakerProcess;
                        break;

                    case StrategyState.MakerProcess:
                        COrder order = DctLegToOrder[MakerLeg];
                        if (order.Status.Equals(COrder.OrderState.Filled))
                        {
                            State = StrategyState.TakerSend;
                            goto case StrategyState.TakerSend;
                        }
                        else if (Profit < profitUSD && order.canCancel())
                        {
                            order.cancel();
                            State = Continuous ? StrategyState.Continuous : StrategyState.Active;
                            DctLegToOrder.Clear();
                        }
                        break;

                    case StrategyState.TakerSend:
                        for (int currentLeg = 1; currentLeg <= dctLegs.Count; currentLeg++)
                        {
                            if (!DctLegToOrder.ContainsKey(currentLeg))
                            {
                                OrderSide sideTaker = dctLegs[currentLeg].Item1;
                                CProduct productTaker = dctLegs[currentLeg].Item2;
                                Double sizeTaker = GetSize(sideTaker, productTaker);
                                Double priceTaker = ((Double)productTaker.Bid + (Double)productTaker.Ask) / 2.0;
                                CurrentLeg = currentLeg;
                                dctLegs[currentLeg].Item2.Exchange.trade(this, currentLeg, sideTaker, productTaker, Math.Round(sizeTaker, productTaker.PrecisionSize), Math.Round(priceTaker, productTaker.PrecisionPrice));
                            }
                        }
                        if (DctLegToOrder.Count >= 3)
                            State = StrategyState.TakerProcess;
                        break;

                    case StrategyState.TakerProcess:
                        Boolean allFilled = true;
                        for (int currentLeg = 1; currentLeg <= dctLegs.Count; currentLeg++)
                        {
                            if (DctLegToOrder.ContainsKey(currentLeg))
                            {
                                COrder orderTaker = DctLegToOrder[currentLeg];
                                if (orderTaker.Status.Equals(COrder.OrderState.Sent) || orderTaker.Status.Equals(COrder.OrderState.Cancelled))
                                {
                                    allFilled = false;
                                }
                                else if (orderTaker.canCancel())
                                {
                                    allFilled = false;
                                    CExchange exchange = orderTaker.Exchange;
                                    OrderSide sideTaker = orderTaker.Side;
                                    CProduct productTaker = orderTaker.Product;
                                    Double sizeTaker = orderTaker.Size;
                                    Double priceTaker = ((Double)productTaker.Bid + (Double)productTaker.Ask) / 2.0;
                                    CurrentLeg = currentLeg;
                                    orderTaker.cancel();
                                    COrder orderCancel;
                                    DctLegToOrder.TryRemove(currentLeg, out orderCancel);
                                    exchange.trade(this, currentLeg, sideTaker, productTaker, Math.Round(sizeTaker, productTaker.PrecisionSize), Math.Round(priceTaker, productTaker.PrecisionPrice));
                                }
                            }
                            else
                                allFilled = false;
                        }
                        if (allFilled)
                        {
                            if (Continuous)
                                State = StrategyState.Continuous;
                            else
                                State = StrategyState.Inactive;
                            DctLegToOrder.Clear();
                        }
                        break;
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
            OrderSide side = dctLegs[CurrentLeg].Item1;
            CProduct product = dctLegs[CurrentLeg].Item2;
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
                CProduct productExchange = DctStrategyProducts[productUSD];
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
            dctLegs[CurrentLeg].Item2.Exchange.trade(this, CurrentLeg, side, product, Math.Round(size, product.PrecisionSize), Math.Round(price, product.PrecisionPrice));
            if (++CurrentLeg > dctLegs.Count)
                CurrentLeg = 1;
        }

        public override string ToString()
        {
            return Leg1 + " "  + Leg2 + " " + Leg3;
        }

    }
}
