using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public class CProduct : INotifyPropertyChanged
    {
        public String Symbol { get; set; }
        public CExchange Exchange { get; set; }
        public Decimal? Bid { get; set; }
        public Decimal? Ask { get; set; }
        public Decimal? Last { get; set; }
        public Decimal? InitialLast { get; set; }
        public Decimal? Volume { get; set; }
        public DateTime? TimeStampLastTick { get; set; }
        public DateTime? TimeStampLastBalance { get; set; }
        public Double? MinSize { get; set; }
        public int PrecisionSize { get; set; }
        public int PrecisionPrice { get; set; }
        public MTObservableCollection<CStrategy> colStrategy;

        public void SetLast(Decimal last)
        {
            if (!InitialLast.HasValue)
                this.InitialLast = last;
            this.Last = last;
        }

        public Double? InitialBalance { get; set; }
        public Double? CurrentBalance { get; set; }
        public void SetBalance(Double balance)
        {
            if (!InitialBalance.HasValue)
                this.InitialBalance = balance;

            if (CurrentBalance.HasValue && balance != CurrentBalance)
            {
                Decimal? price = (Last.HasValue ? Last : Bid.HasValue && Ask.HasValue ? (Bid + Ask) / 2 : null);
                if (price.HasValue)
                    TradingPnL += (price.GetValueOrDefault() * (Decimal)(balance - CurrentBalance.GetValueOrDefault()));
            }
            this.CurrentBalance = balance;
        }

        public Decimal InitialUSD
        {
            get { return InitialLast.GetValueOrDefault() * (Decimal)InitialBalance.GetValueOrDefault(); }
        }
        public Decimal CurrentUSD
        {
            get { return Last.GetValueOrDefault() * (Decimal)CurrentBalance.GetValueOrDefault(); }
        }

        public Decimal TotalPnL
        {
            get { return CurrentUSD - InitialUSD; }
        }

        public Double ChangeQty
        {
            get { return CurrentBalance.GetValueOrDefault() - InitialBalance.GetValueOrDefault(); }
        }

        public Decimal TradingPnL
        {
            get; private set;
        }

        public int SignPnL
        {
            get { return Math.Sign(TotalPnL); }
        }


        public CProduct(CExchange exchange, String symbol, int precisionSize, int precisionPrice)
        {
            Symbol = symbol;
            this.Exchange = exchange;
            this.PrecisionSize = precisionSize;
            this.PrecisionPrice = precisionPrice;
            colStrategy = new MTObservableCollection<CStrategy>();
            TradingPnL = 0;
            if (Symbol.Equals("USDT"))
                SetLast(1);
            if (Symbol.Contains("LTC") && Symbol.Contains("BTC"))
                MinSize = 0.01;
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

        public void updateGrid(String field)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(field));
        }

        public virtual void updateGUI()
        {
            this.OnPropertyChanged("InitialBalance");
            this.OnPropertyChanged("CurrentBalance");
            this.OnPropertyChanged("TimeStampLastTick");
            this.OnPropertyChanged("TimeStampLastBalance");
            this.OnPropertyChanged("InitialLast");
            this.OnPropertyChanged("Last");
            this.OnPropertyChanged("CurrentUSD");
            this.OnPropertyChanged("TotalPnL");
            this.OnPropertyChanged("TradingPnL");
            this.OnPropertyChanged("SignPnL");
        }

        public override String ToString()
        {
            return Symbol;
        }
    }
}
