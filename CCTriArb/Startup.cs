﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CCTriArb
{
    class Startup
    {
        //[STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Startup at " + DateTime.Now);
            CStrategyServer server = new CStrategyServer(args);
            while (server.IsActive)
                System.Threading.Thread.Sleep(1000);

        }
    }
}
