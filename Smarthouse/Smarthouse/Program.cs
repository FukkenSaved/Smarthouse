﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Smarthouse
{
    class Program
    {
        static void Main(string[] args)
        {

            System.Console.WriteLine("Hello world!");
            //init core
            Core core;
            Thread core_thread = new Thread(delegate() { core = new Core(); });
            core_thread.Start();
            do
            {
                //System.Console.WriteLine("I'm alive! Main thread!");//lol. Don't write Console.WriteLine... Or it will be funny overflow exception :D
                Thread.Sleep(10000);
            }while(true);
            
        }
    }
}
