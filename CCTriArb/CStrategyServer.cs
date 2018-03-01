using Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace CCTriArb
{

    public class CStrategyServer
    {
        public MTObservableCollection<CTriArb> colStrategies;
        public MTObservableCollection<COrder> colOrders;
        public ConcurrentDictionary<String, COrder> dctIdToOrder;

        public Dictionary<String, CExchange> dctExchanges;
        public Dictionary<String, CProduct> dctProducts;

        public CCTriArbMain gui;
        public ServerType serverType = ServerType.Debugging;

        public bool IsActive { get; set; }
        public Double? TradeUSD { get; set; }
        public Double? MinProfit { get; set; }

        public SynchronizationContext UIContext { get; set; }

        public static CStrategyServer Server
        {
            get;
            private set;
        }

        public CStrategyServer()
        {
            Server = this;

            IsActive = true;
            TradeUSD = 1.0;
            MinProfit = 0.002;
            CExchange kuExchange = new CKuCoin();
            CExchange beExchange = new CBinance();

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

            //Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            colStrategies = new MTObservableCollection<CTriArb>();
            colStrategies.Add(new CTriArb(dctLegs_ku_USD_BTC_ETH));
            colStrategies.Add(new CTriArb(dctLegs_ku_USD_ETH_BTC));
            colStrategies.Add(new CTriArb(dctLegs_be_USD_BTC_ETH));
            colStrategies.Add(new CTriArb(dctLegs_be_USD_ETH_BTC));

            colOrders = new MTObservableCollection<COrder>();
            dctIdToOrder = new ConcurrentDictionary<String, COrder>();

            dctExchanges = new Dictionary<String, CExchange>();
            dctExchanges.Add(kuExchange.Name, kuExchange);
            dctExchanges.Add(beExchange.Name, beExchange);

            System.Timers.Timer timerTicks = new System.Timers.Timer(5000);
            timerTicks.Elapsed += new ElapsedEventHandler(pollTicks);
            timerTicks.Start();

            System.Timers.Timer timerOrders = new System.Timers.Timer(5000);
            timerOrders.Elapsed += new ElapsedEventHandler(pollOrders);
            timerOrders.Start();

            System.Timers.Timer timerStrategy = new System.Timers.Timer(5000);
            timerStrategy.Elapsed += new ElapsedEventHandler(cycleStrategy);
            timerStrategy.Start();

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
                gui = new CCTriArbMain();
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

        private void cycleStrategy(object source, ElapsedEventArgs e)
        {
            foreach (var strategy in colStrategies)
            {
                strategy.cycleStrategy();
            }
        }
    }
}
