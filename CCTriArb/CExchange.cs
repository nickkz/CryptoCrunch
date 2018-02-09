using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CCTriArb
{
    public abstract class CExchange
    {
        #region member variables
        protected static String BASE_CURRENCY = "USD";
        protected static String USER_AGENT = @"Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";
        protected String API_KEY, API_SECRET, API_TIMESTAMP, API_PASSPHRASE;
        #endregion

        #region Properties
        public ObservableCollection <CProduct> colProducts { get; set; }
        public Dictionary<String, double> dctPositions { get; set; }
        public Dictionary<String, String> dctAccounts { get; set; }
        public String Name { get; set; }
        public string BaseURL { get; set; }
        public string AccountRequest { get; set; }
        public string OrderRequest { get; set; }
        public string TickRequest { get; set; }
        protected CStrategyServer Server { get; set; }
        #endregion

        #region Constructor
        public CExchange(CStrategyServer server)
        {
            colProducts = new ObservableCollection<CProduct>();
            dctAccounts = new Dictionary<String, String>();
            dctPositions = new Dictionary<String, double>();
            this.Server = server;
        }
        #endregion

        #region methods
        public abstract void pollTicks(object source, ElapsedEventArgs e);
        public abstract void pollOrders(object source, ElapsedEventArgs e);
        public abstract void getAccounts();
        public abstract void trade(ServerType serverType, OrderSide? side, CProduct product, Double size, Decimal? price);
        public abstract void cancel(String orderID);
        public abstract void cancelAll();

        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
}
