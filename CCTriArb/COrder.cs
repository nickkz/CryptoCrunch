using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public class COrder : INotifyPropertyChanged
    {
        public String OrderID { get; set; }
        public String OID { get; set; }
        public Double Fee { get; set; }
        public Double FeeRate { get; set; }
        public CProduct Product { get; set; }
        public OrderSide Side { get; set; }

        public Double Price { get; set; }
        public Double DealPrice { get; set; }

        public Double Size { get; set; }
        public Double Filled { get; set; }
        public String Status { get; set; }
        public Double DealValue { get; set; }

        public CStrategy Strategy { get; set; }
        public CExchange Exchange { get; set; }

        public DateTime? TimeStampSent { get; set; }
        public DateTime? TimeStampLastUpdate { get; set; }
        public DateTime? TimeStampFilled { get; set; }

        public COrder(String orderID)
        {
            this.OrderID = orderID;
            this.TimeStampLastUpdate = DateTime.Now;
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
            this.OnPropertyChanged("Status");
            this.OnPropertyChanged("TimeStampSent");
            this.OnPropertyChanged("TimeStampLastUpdate");
            this.OnPropertyChanged("TimeStampFilled");
            this.OnPropertyChanged("Filled");
            this.OnPropertyChanged("DealPrice");
            this.OnPropertyChanged("Fee");
            this.OnPropertyChanged("FeeRate");
        }

        public void cancel()
        {
            this.Exchange.cancel(this.OrderID);
        }
           

    }
}
