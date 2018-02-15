using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace CCTriArb
{

    public class CStrategyServer
    {
        public ObservableCollection<CTriArb> colStrategies;
        public Dictionary<String, CExchange> dctExchanges;
        public Dictionary<String, CProduct> dctProducts;
        public ObservableCollection<COrder> colOrders;
        public Dictionary<String, COrder> dctIdToOrder;
        public CCTriArbMain gui;
        public ServerType serverType = ServerType.Debugging;

        public bool IsActive { get; set; }

        public CStrategyServer()
        {
            IsActive = true;
            CExchange kuExchange = new CKuCoin(this);
            CExchange beExchange = new CBinance(this);

            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_ku_USD_BTC_ETH = new Dictionary<int, Tuple<OrderSide, CProduct>>();
            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_ku_USD_ETH_BTC = new Dictionary<int, Tuple<OrderSide, CProduct>>();
            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_be_USD_BTC_ETH = new Dictionary<int, Tuple<OrderSide, CProduct>>();
            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_be_USD_ETH_BTC = new Dictionary<int, Tuple<OrderSide, CProduct>>();

            dctProducts = new Dictionary<String, CProduct>();
            CProduct product_ku_BTC_USDT = new CProduct(kuExchange, "BTC-USDT", 6, 8);
            CProduct product_ku_ETH_BTC = new CProduct(kuExchange, "ETH-BTC", 6, 6);
            CProduct product_ku_ETH_USDT = new CProduct(kuExchange, "ETH-USDT", 6, 6);
            CProduct product_be_BTC_USDT = new CProduct(beExchange, "BTCUSDT", 6, 8);
            CProduct product_be_ETH_BTC = new CProduct(beExchange, "ETHBTC", 3, 6);
            CProduct product_be_ETH_USDT = new CProduct(beExchange, "ETHUSDT", 4, 6);

            dctProducts.Add(product_ku_BTC_USDT.Symbol, product_ku_BTC_USDT);
            dctProducts.Add(product_ku_ETH_BTC.Symbol, product_ku_ETH_BTC);
            dctProducts.Add(product_ku_ETH_USDT.Symbol, product_ku_ETH_USDT);
            dctProducts.Add(product_be_BTC_USDT.Symbol, product_be_BTC_USDT);
            dctProducts.Add(product_be_ETH_BTC.Symbol, product_be_ETH_BTC);
            dctProducts.Add(product_be_ETH_USDT.Symbol, product_be_ETH_USDT);

            dctLegs_ku_USD_BTC_ETH.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_BTC_USDT));
            dctLegs_ku_USD_BTC_ETH.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_ETH_BTC));
            dctLegs_ku_USD_BTC_ETH.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_ETH_USDT));
            dctLegs_ku_USD_ETH_BTC.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_ETH_USDT));
            dctLegs_ku_USD_ETH_BTC.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_ETH_BTC));
            dctLegs_ku_USD_ETH_BTC.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_BTC_USDT));

            dctLegs_be_USD_BTC_ETH.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_be_BTC_USDT));
            dctLegs_be_USD_BTC_ETH.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_be_ETH_BTC));
            dctLegs_be_USD_BTC_ETH.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_be_ETH_USDT));
            dctLegs_be_USD_ETH_BTC.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_be_ETH_USDT));
            dctLegs_be_USD_ETH_BTC.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_be_ETH_BTC));
            dctLegs_be_USD_ETH_BTC.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_be_BTC_USDT));

            colStrategies = new ObservableCollection<CTriArb>();
            colStrategies.Add(new CTriArb(this, dctLegs_ku_USD_BTC_ETH));
            colStrategies.Add(new CTriArb(this, dctLegs_ku_USD_ETH_BTC));
            colStrategies.Add(new CTriArb(this, dctLegs_be_USD_BTC_ETH));
            colStrategies.Add(new CTriArb(this, dctLegs_be_USD_ETH_BTC));

            dctExchanges = new Dictionary<String, CExchange>();
            dctExchanges.Add(kuExchange.Name, kuExchange);
            dctExchanges.Add(beExchange.Name, beExchange);

            colOrders = new ObservableCollection<COrder>();
            dctIdToOrder = new Dictionary<String, COrder>();

            System.Timers.Timer timerTicks = new System.Timers.Timer(5000);
            timerTicks.Elapsed += new ElapsedEventHandler(pollTicks);
            timerTicks.Start();

            System.Timers.Timer timerOrders = new System.Timers.Timer(5000);
            timerOrders.Elapsed += new ElapsedEventHandler(pollOrders);
            timerOrders.Start();

            // open gui
            openGui();
        }

        public void AddLog(String logText)
        {
            gui.AddLog(logText);
        }

        [STAThread]
        private void openGui()
        {
            Thread newWindowThread = new Thread(new ThreadStart(() =>
            {
                // Create and show the Window
                gui = new CCTriArbMain(this);
                gui.Show();
                // Start the Dispatcher Processing
                System.Windows.Threading.Dispatcher.Run();
            }));

            // Set the apartment state
            newWindowThread.SetApartmentState(ApartmentState.STA);
            // Make the thread a background thread
            newWindowThread.IsBackground = true;
            // Start the thread
            newWindowThread.Start();
        }


        private void pollTicks(object source, ElapsedEventArgs e)
        {
            foreach (var exchange in dctExchanges.Values)
            {
                exchange.pollTicks(source, e);
            }
        }

        private void pollOrders(object source, ElapsedEventArgs e)
        {
            foreach (var exchange in dctExchanges.Values)
            {
                exchange.pollOrders(source, e);
            }
        }
    }
}
