using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    public class CProduct
    {
        public String Symbol { get; set; }
        public CExchange Exchange { get; set; }
        public Decimal? Bid { get; set; }
        public Decimal? Ask { get; set; }
        public Decimal? Last { get; set; }
        public Decimal? Volume { get; set; }
        public DateTime DtUpdate { get; set; }
        public ObservableCollection<CStrategy> colStrategy;

        public CProduct(CExchange exchange, String symbol)
        {
            Symbol = symbol;
            this.Exchange = exchange;
            colStrategy = new ObservableCollection<CStrategy>();
        }

        public bool subscribe()
        {
            return true;
        }
    }
}
