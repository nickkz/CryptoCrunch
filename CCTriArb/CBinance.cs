using ExchangeSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CCTriArb
{
    class CBinance : CESExchange
    {
        public CBinance() : base()
        {
            API_KEY = Properties.Settings.Default.BINANCE_API_KEY;
            API_SECRET = Properties.Settings.Default.BINANCE_API_SECRET;
            api = new ExchangeBinanceAPI();
            api.LoadAPIKeysUnsecure(API_KEY, API_SECRET);
            Name = api.Name;
            getAccounts();
        }

        public override String exchangeUSD(String coinType)
        {
            return (coinType.Equals("USDT")) ? coinType : coinType + "USDT";
        }
    }
}
