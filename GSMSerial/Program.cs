using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using GsmComm.PduConverter;
using System.Threading;

namespace GSMSerial
{
    class Program
    {
        const int MAX_MODULES_COUNT = 16;
        const int MAIN_DELAY        = 60000 * 5; //Delay 5 minutes between checking new modules

        static List<Module> modules = new List<Module>(MAX_MODULES_COUNT);

        static void Main(string[] args)
        {
            while (true)
            {
                if (SerialPort.GetPortNames().Length > 0)
                {
                    foreach (string serialPort in SerialPort.GetPortNames())
                    {
                        GsmCommunication com = new GsmCommunication(serialPort);

                        try
                        {
                            com.OpenPort();

                            if (com.CheckPort())
                            {
                                string phoneNumber = com.GetNumber();
                                string imei = com.GetImei();
                                Console.WriteLine("[Main] Port {0} is available for use.", serialPort);
                                Console.WriteLine("[Main] Port {0} SIM card number is: {1}", serialPort, phoneNumber);
                                Console.WriteLine("[Main] Port {0} IMEI number: {1}", serialPort, imei);

                                Module GSM      = new Module(serialPort);
                                GSM.IMEI        = imei;
                                GSM.PhoneNumber = phoneNumber;

                                //Find existing module by IMEI - if not found we add new 
                                if (!modules.Exists(ByIMEI(imei))) {
                                    modules.Add(GSM);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("[Main][Error] exception occured: {0}", e.Message);
                        }
                        finally
                        {
                            com.ClosePort();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[Main] No available COM ports for use now.");
                }

                foreach(var module in modules)
                {
                    if (!module.Running)
                    {
                        module.Start();
                        module.Running = true;
                    }
                }

                Console.WriteLine("[Main][Info] Modules total connected: {0}", modules.Count);

                Thread.Sleep(MAIN_DELAY);
            }
            

           // IncomingSmsPdu sms = IncomingSmsPdu.Decode("0791947101570000040B917321352101F00000023022515312800EC926F429A5069D5410B5A8CD02", true);
            //byte[] getuserdata = sms.GetUserDataTextWithoutHeader()
            //string userdata = Encoding.UTF7.GetString(getuserdata);


            

          
           // Console.WriteLine(sms.GetTimestamp());
          //  Console.WriteLine(sms.UserDataText);

           // ShowOpenComPorts();
            Console.ReadKey();
        }

        static void GetAvailableComPorts()
        {
            Console.WriteLine("Open Serial/COM ports for use:");
            if (SerialPort.GetPortNames().Length > 0)
            {
                foreach (string serialPort in SerialPort.GetPortNames())
                {
                    GsmCommunication com = new GsmCommunication(serialPort);

                    try
                    {
                        com.OpenPort();

                        if(com.CheckPort())
                        {
                            string phoneNumber = com.GetNumber();
                            string imei        = com.GetImei();
                            Console.WriteLine("Port {0} is available for use.", serialPort);
                            Console.WriteLine("Port {0} SIM card number is: {1}", serialPort, phoneNumber);
                            Console.WriteLine("Port {0} IMEI number: {1}", serialPort, imei);
                        }
                        
                    }
                    catch(Exception e) {
                        Console.WriteLine("[Error] exception occured: {0}", e.Message);
                    }
                    finally {
                         com.ClosePort();
                    }
                }
            }
            else
            {
                Console.WriteLine("No available COM ports for use now.");
            }
        }

        static Predicate<Module> ByIMEI(string imei)
        {
            return delegate (Module sim)
            {
                return sim.IMEI == imei;
            };
        }

        class Module
        {
            private GsmCommunication module;
            private int delay = 6000;

            public bool Running { get; set; }

            public string PhoneNumber { get; set; }

            public string IMEI { get; set; }

            public Module(string portName)
            {
                module = new GsmCommunication(portName);
            }

            public void Start()
            {
              module.OpenPort();
              module.idleReceiveStart();

              Task.Factory.StartNew(
              () =>
              {
                  while (true)
                  {
                      Thread.Sleep(delay);
                      CheckStatus();
                  }
              },
              CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            public void CheckStatus()
            {
                module.idleReceiveStop();

                if (module.CheckPort())
                {
                    Console.WriteLine("[{0}] Module is working correctly, health check passed.", IMEI);
                }

                module.idleReceiveStart();
            }
           
        }

        static void ShowComPorts()
        {
            Console.WriteLine("Total Serial/COM ports in system:");
            if (SerialPort.GetPortNames().Length > 0)
            {
                foreach (string s in SerialPort.GetPortNames())
                {
                    Console.WriteLine(s);
                }
            }
            else
            {
                Console.WriteLine("No COM ports detected in system for now.");
            }
        }
    }
}
