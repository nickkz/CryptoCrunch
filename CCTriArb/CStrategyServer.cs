using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using YamlDotNet.RepresentationModel;

namespace CCTriArb
{

    public class CStrategyServer
    {
        public MTObservableCollection<CTriArb> colStrategies;
        public MTObservableCollection<COrder> colServerOrders;
        public ConcurrentDictionary<String, COrder> dctIdToOrder;
        public Dictionary<String, CExchange> dctExchanges;

        public MTObservableCollection<CProduct> colServerProducts;
        public Dictionary<String, CProduct> dctServerProducts;

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

        public CStrategyServer(string[] args)
        {
            Server = this;
            IsActive = true;
            TradeUSD = 1.0;
            MinProfit = 0.002;
            CExchange kuExchange = new CKuCoin();
            CExchange beExchange = new CBinance();
            dctExchanges = new Dictionary<String, CExchange>();
            dctExchanges.Add(kuExchange.Name, kuExchange);
            dctExchanges.Add(beExchange.Name, beExchange);
            colServerProducts = new MTObservableCollection<CProduct>();
            dctServerProducts = new Dictionary<String, CProduct>();
            colStrategies = new MTObservableCollection<CTriArb>();

            CExchange exchangeConfig;
            // load yaml config
            string config = File.ReadAllText(args[0]);

            // load Yaml
            var input = new StringReader(config);
            // Load the stream
            var yaml = new YamlStream();
            yaml.Load(input);
            // Examine the stream
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            foreach (var entry in mapping.Children)
            {
                if (entry.Key.ToString().Equals("Exchanges"))
                {
                    foreach (YamlMappingNode exchangeYaml in ((YamlSequenceNode)entry.Value).Children)
                    {
                        exchangeConfig = dctExchanges[exchangeYaml["Exchange"].ToString()];
                        var productsYaml = (YamlSequenceNode)exchangeYaml["Products"];
                        foreach (YamlMappingNode productYaml in productsYaml)
                        {
                            String symbol = productYaml.Children[new YamlScalarNode("symbol")].ToString();
                            int precisionSize = int.Parse(productYaml.Children[new YamlScalarNode("precisionSize")].ToString());
                            int precisionPrice = int.Parse(productYaml.Children[new YamlScalarNode("precisionPrice")].ToString());
                            CProduct productConfig = new CProduct(exchangeConfig, symbol, precisionSize, precisionPrice);
                            dctServerProducts.Add(exchangeConfig + "." + symbol, productConfig);
                            if (symbol.Contains("USD"))
                                colServerProducts.Add(productConfig);
                            productConfig.Exchange.dctExchangeProducts.Add(symbol, productConfig);
                        }
                        var strategiesYaml = (YamlSequenceNode)exchangeYaml["Strategies"];
                        foreach (YamlMappingNode strategyYaml in strategiesYaml)
                        {
                            Dictionary<int, Tuple<OrderSide, CProduct>> dctLegs_config = new Dictionary<int, Tuple<OrderSide, CProduct>>();
                            for (int leg = 1;leg <= 3;leg++)
                            {
                                String legsymbol = strategyYaml.Children[new YamlScalarNode("leg" + leg + "symbol")].ToString();
                                CProduct strategyProduct = dctServerProducts[exchangeConfig + "." + legsymbol];
                                Enum.TryParse(strategyYaml.Children[new YamlScalarNode("leg" + leg + "side")].ToString(), out OrderSide orderSide);
                                dctLegs_config.Add(leg, new Tuple<OrderSide, CProduct>(orderSide, strategyProduct));
                            }
                            int makerLeg = int.Parse(strategyYaml.Children[new YamlScalarNode("makerleg")].ToString());
                            colStrategies.Add(new CTriArb(dctLegs_config, makerLeg));
                        }
                    }
                }
            }

            colServerOrders = new MTObservableCollection<COrder>();
            dctIdToOrder = new ConcurrentDictionary<String, COrder>();

            System.Timers.Timer timerTicks = new System.Timers.Timer(5000);
            timerTicks.Elapsed += new ElapsedEventHandler(pollTicks);
            timerTicks.Start();

            System.Timers.Timer timerOrders = new System.Timers.Timer(5000);
            timerOrders.Elapsed += new ElapsedEventHandler(pollOrders);
            timerOrders.Start();

            System.Timers.Timer timerPositions = new System.Timers.Timer(5000);
            timerPositions.Elapsed += new ElapsedEventHandler(pollPositions);
            timerPositions.Start();

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

        private void pollPositions(object source, ElapsedEventArgs e)
        {
            foreach (var exchange in dctExchanges.Values)
            {
                exchange.pollPositions(source, e);
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
