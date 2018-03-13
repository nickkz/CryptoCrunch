using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCTriArb
{
    class CGDAX : CESExchange
    {
        public CGDAX() : base()
        {
            API_KEY = Properties.Settings.Default.GDAX_API_KEY;
            API_SECRET = Properties.Settings.Default.GDAX_API_SECRET;
            API_PASSPHRASE = Properties.Settings.Default.GDAX_API_PASSPHRASE;
            api = new ExchangeGdaxAPI();
            api.LoadAPIKeysUnsecure(API_KEY, API_SECRET, API_PASSPHRASE);
            Name = api.Name;
            getAccounts();
        }

        public override String exchangeUSD(String coinType)
        {
            return (coinType.Equals("USD")) ? "USDT" : coinType + "-USD";
        }
    }
}
