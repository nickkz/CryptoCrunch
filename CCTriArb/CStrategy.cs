using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public abstract class CStrategy : INotifyPropertyChanged
    {
        protected CStrategyServer server;
        protected int currentLeg;
        protected Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs;
        public Dictionary<String, CProduct> DctProducts { get; set; }
        public Dictionary<String, COrder> DctOrders { get; set; }
        public Dictionary<Int64, COrder> DctLegToOrder { get; set; }
        protected Boolean processStrategy;

        protected CStrategy(CStrategyServer server, Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs)
        {
            this.server = server;
            this.dctLegs = dctLegs;
            processStrategy = false;
            currentLeg = 1;
            DctProducts = new Dictionary<String, CProduct>();
            DctOrders = new Dictionary<String, COrder>();
            DctLegToOrder = new Dictionary<Int64, COrder>();

            // link everything up
            for (int i = 1; i <= dctLegs.Count; i++)
            {
                // assign Strategy to Leg
                dctLegs[i].Item2.colStrategy.Add(this);

                // assign Product to Global Product Collection
                CProduct product = dctLegs[i].Item2;
                if (!DctProducts.ContainsKey(product.Symbol))
                    DctProducts.Add(product.Symbol, product);

                // assign Product to Exchange Product Collection 
                if (!dctLegs[i].Item2.Exchange.dctProducts.ContainsKey(product.Symbol))
                    dctLegs[i].Item2.Exchange.dctProducts.Add(product.Symbol, product);

                //TODO: any more linking
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public String Leg1 { get { return GetLegDescription(1); } }
        public String Leg2 { get { return GetLegDescription(2); } }
        public String Leg3 { get { return GetLegDescription(3); } }
        public String Leg4 { get { return GetLegDescription(4); } }

        public Decimal? Leg1Bid { get { return GetLegValue(1, "Bid"); } }
        public Decimal? Leg2Bid { get { return GetLegValue(2, "Bid"); } }
        public Decimal? Leg3Bid { get { return GetLegValue(3, "Bid"); } }
        public Decimal? Leg4Bid { get { return GetLegValue(4, "Bid"); } }

        public Decimal? Leg1Ask { get { return GetLegValue(1, "Ask"); } }
        public Decimal? Leg2Ask { get { return GetLegValue(2, "Ask"); } }
        public Decimal? Leg3Ask { get { return GetLegValue(3, "Ask"); } }
        public Decimal? Leg4Ask { get { return GetLegValue(4, "Ask"); } }

        public Decimal? Leg1Last { get { return dctLegs[1].Item2.Last; } }
        public Decimal? Leg2Last { get { return dctLegs[2].Item2.Last; } }
        public Decimal? Leg3Last { get { return dctLegs[3].Item2.Last; } }
        public Decimal? Leg4Last { get { return dctLegs[4].Item2.Last; } }

        public Decimal? Leg1Volume { get { return dctLegs[1].Item2.Volume; } }
        public Decimal? Leg2Volume { get { return dctLegs[2].Item2.Volume; } }
        public Decimal? Leg3Volume { get { return dctLegs[3].Item2.Volume; } }
        public Decimal? Leg4Volume { get { return dctLegs[4].Item2.Volume; } }

        public void updateGrid(String field)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(field));
        }

        protected String GetLegDescription(int leg)
        {
            try
            {
                if (dctLegs == null)
                {
                    return "Waiting...";
                }
                else if (dctLegs.Count < leg)
                {
                    return "Waiting...";
                }
                else
                {
                    Tuple<OrderSide, CProduct> t = dctLegs[leg];
                    return t.Item1.ToString() + " " + t.Item2.Symbol.ToString();
                }
            }
            catch (Exception ex)
            {
                server.gui.AddLog(ex.Message);
                return "";
            }
        }

        protected OrderSide? GetLegSide(int leg)
        {
            try
            {
                if (dctLegs == null)
                {
                    return null;
                }
                else if (dctLegs.Count < leg)
                {
                    return null;
                }
                else
                {
                    Tuple<OrderSide, CProduct> t = dctLegs[leg];
                    return t.Item1;
                }
            }
            catch (Exception ex)
            {
                server.gui.AddLog(ex.Message);
                return null;
            }
        }

        protected Decimal? GetLegValue(int leg, String field)
        {
            try
            {
                if (dctLegs == null)
                {
                    return null;
                }
                else if (dctLegs.Count < leg)
                {
                    return null;
                }
                else
                {
                    Tuple<OrderSide, CProduct> t = dctLegs[leg];
                    switch (field)
                    {
                        case "Bid":
                            return t.Item2.Bid;
                        case "Ask":
                            return t.Item2.Ask;
                        case "Last":
                            return t.Item2.Last;
                        case "Volume":
                            return t.Item2.Volume;
                    }
                    return null;

                }
            }
            catch (Exception ex)
            {
                server.gui.AddLog(ex.Message);
                return null;
            }
        }

        public virtual void updateGUI()
        {
            for (int i = 1; i <= dctLegs.Count; i++)
            {
                this.OnPropertyChanged("Leg" + i);
                this.OnPropertyChanged("Leg" + i + "Bid");
                this.OnPropertyChanged("Leg" + i + "Ask");
            }
        }

        public abstract void cycleStrategy();

    }

}
