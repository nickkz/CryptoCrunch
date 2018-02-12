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
        public int PrecisionSize { get; set; }
        public int PrecisionPrice { get; set; }
        public ObservableCollection<CStrategy> colStrategy;

        public CProduct(CExchange exchange, String symbol, int precisionSize, int precisionPrice)
        {
            Symbol = symbol;
            this.Exchange = exchange;
            this.PrecisionSize = precisionSize;
            this.PrecisionPrice = precisionPrice;
            colStrategy = new ObservableCollection<CStrategy>();
        }

        public override String ToString()
        {
            return Symbol;
        }
    }
}
