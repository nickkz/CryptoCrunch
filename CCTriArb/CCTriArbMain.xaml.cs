using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CCTriArb
{
    /// <summary>
    /// Interaction logic for CCTriArbMain.xaml
    /// </summary>
    public partial class CCTriArbMain : Window
    {
        CStrategyServer server;
        public CCTriArbMain()
        {
            InitializeComponent();
            this.server = CStrategyServer.Server;
        }

        public void AddLog(String logText)
        {

            this.Dispatcher.Invoke((Action)(() =>
            {
                txtLog.AppendText(logText + "\n");
            }));
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            server.IsActive = false;
            Close();
        }


        private void frmTriArbMain_Loaded(object sender, RoutedEventArgs e)
        {
            dgStrategies.DataContext = server.colStrategies;
            dgOrders.DataContext = server.colOrders;
            dgPositions.DataContext = server.colServerProducts;
        }

        private void btnTradeNextPassive_Click(object sender, RoutedEventArgs e)
        {
            int selectedRow = dgStrategies.SelectedIndex;
            Double size = 1;
            bool parseAmount = Double.TryParse(txtUSD.Text, out size);
            if (selectedRow > -1)
            {
                CTriArb triarb = server.colStrategies[selectedRow];
                triarb.tradeNext(server.serverType, size, false);
            }
        }

        private void btnTradeNextActive_Click(object sender, RoutedEventArgs e)
        {
            int selectedRow = dgStrategies.SelectedIndex;
            Double size = 1;
            bool parseAmount = Double.TryParse(txtUSD.Text, out size);
            if (selectedRow > -1)
            {
                CTriArb triarb = server.colStrategies[selectedRow];
                triarb.tradeNext(server.serverType, size, true);
            }

        }

        private void cboServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (server != null)
            {
                Enum.TryParse(((ComboBoxItem)cboServer.SelectedItem).Content.ToString(), out ServerType serverType);
                server.serverType = serverType;
            }
        }

        private void btnCancelAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (CExchange exchange in server.dctExchanges.Values)
            {
                exchange.cancelAll();
            }
        }

        private void btnTradeSingle_Click(object sender, RoutedEventArgs e)
        {

            foreach (CTriArb triarb in dgStrategies.SelectedItems)
            {
                AddLog(triarb + " Activated!");
                triarb.activateStrategy(false);
            }
        }

        private void txtUSD_TextChanged(object sender, TextChangedEventArgs e)
        {
            Double size;
            bool parseAmount = Double.TryParse(txtUSD.Text, out size);
            if (server != null)
                server.TradeUSD = size;
        }

        private void txtMinProfit_TextChanged(object sender, TextChangedEventArgs e)
        {
            Double minProfit;
            bool parseAmount = Double.TryParse(txtMinProfit.Text, out minProfit);
            if (server != null)
                server.MinProfit = minProfit;
        }

        private void btnTradeContinuous_Click(object sender, RoutedEventArgs e)
        {
            foreach (CTriArb triarb in dgStrategies.SelectedItems)
            {
                AddLog(triarb + " Continuous!");
                triarb.activateStrategy(true);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            foreach (COrder order in dgOrders.SelectedItems)
            {
                AddLog(order + " Cancelling!");
                order.cancel();
            }
        }
    }
}
