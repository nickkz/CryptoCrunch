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
        public ObservableCollection<CExchange> colExchanges;
        public ObservableCollection<CProduct> colProducts;
        public ObservableCollection<COrder> colOrders;
        public Dictionary<Guid, COrder> dctIdToOrder;
        public CCTriArbMain gui;

        public bool IsActive { get; set; }

        public CStrategyServer()
        {
            IsActive = true;
            CExchange kuExchange = new CKuCoin(this);

            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_USD_BTC_ETH = new Dictionary<int, Tuple<OrderSide, CProduct>>();
            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_USD_ETH_BTC = new Dictionary<int, Tuple<OrderSide, CProduct>>();

            colProducts = new ObservableCollection<CProduct>();
            CProduct product_ku_BTC_USDT = new CProduct(kuExchange, "BTC-USDT");
            CProduct product_ku_ETH_BTC = new CProduct(kuExchange, "ETH-BTC");
            CProduct product_ku_ETH_USDT = new CProduct(kuExchange, "ETH-USDT");

            colProducts.Add(product_ku_BTC_USDT);
            colProducts.Add(product_ku_ETH_BTC);
            colProducts.Add(product_ku_ETH_USDT);

            dctLegs_USD_BTC_ETH.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_BTC_USDT));
            dctLegs_USD_BTC_ETH.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_ETH_BTC));
            dctLegs_USD_BTC_ETH.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_ETH_USDT));

            dctLegs_USD_ETH_BTC.Add(1, new Tuple<OrderSide, CProduct>(OrderSide.Buy, product_ku_ETH_USDT));
            dctLegs_USD_ETH_BTC.Add(2, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_ETH_BTC));
            dctLegs_USD_ETH_BTC.Add(3, new Tuple<OrderSide, CProduct>(OrderSide.Sell, product_ku_BTC_USDT));

            colStrategies = new ObservableCollection<CTriArb>();
            colStrategies.Add(new CTriArb(this, dctLegs_USD_BTC_ETH));
            colStrategies.Add(new CTriArb(this, dctLegs_USD_ETH_BTC));

            colExchanges = new ObservableCollection<CExchange>();
            colExchanges.Add(kuExchange);

            colOrders = new ObservableCollection<COrder>();
            dctIdToOrder = new Dictionary<Guid, COrder>();

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
            foreach (var exchange in colExchanges)
            {
                exchange.pollTicks(source, e);
            }

        }

        private void pollOrders(object source, ElapsedEventArgs e)
        {
            foreach (var exchange in colExchanges)
            {
                exchange.pollOrders(source, e);
            }
        }



    }
}
