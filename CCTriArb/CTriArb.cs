﻿using CryptoCrunch;
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

        internal CTriArb(CStrategyServer server, Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs) : base(server, dctLegs)
        {
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

        public override void updateGUI()
        {
            base.updateGUI();
            this.OnPropertyChanged("ProfitAAA");
            this.OnPropertyChanged("ProfitPAA");
            this.OnPropertyChanged("ProfitPPA");
            this.OnPropertyChanged("ProfitPPP");
        }

        public void tradeNext(ServerType serverType, Boolean active)
        {
            OrderSide side = dctLegs[currentLeg].Item1;
            CProduct product = dctLegs[currentLeg].Item2;
            dctLegs[currentLeg].Item2.Exchange.trade(serverType, side, product, 0.00001, side == OrderSide.Buy ? product.Bid : product.Ask);
            if (++currentLeg > dctLegs.Count)
                currentLeg = 1;
        }

    }
}
