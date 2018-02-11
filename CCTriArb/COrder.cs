using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public class COrder
    {
        public String OrderID { get; set; }
        public CProduct Product { get; set; }
        public OrderSide Side { get; set; }
        public Double Size { get; set; }
        public Double Executed { get; set; }
        public String Status { get; set; }
        public CStrategy Strategy { get; set; }
        public CExchange Exchange { get; set; }
        public DateTime TimeStamp { get; set; }

        public COrder() {}

        public COrder(String orderID)
        {
            this.OrderID = orderID;
        }
    }
}
