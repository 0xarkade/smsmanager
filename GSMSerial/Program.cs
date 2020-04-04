using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using xNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace GSMSerial
{
    class Program
    {
        /*[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int MessageBox(int hWnd, String text, String caption, uint type);*/

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
            private int delay = 60000;

            public bool Running { get; set; }

            public string PhoneNumber { get; set; }

            public string IMEI { get; set; }

            private List<string> smsHashes = new List<string>();

            private string url = "http://smscrypto.com/api/";
            private string key = "c\\U#EGEeLhc+__Dp3%-4H(W4[*L4!P";


            public Module(string portName)
            {
                module = new GsmCommunication(portName);
            }

            public void Start()
            {
                module.OpenPort();

                //Register phone number on server as it is our first interaction with SIM card
                dynamic phonenum = new JObject();

                phonenum.number = PhoneNumber;
                phonenum.imei = IMEI;

                string registerPhoneJson = JsonConvert.SerializeObject(phonenum);

                RegisterPhone(registerPhoneJson);

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
                if (module.CheckPort())
                {
                    Console.WriteLine("[{0}] Module is working correctly, health check passed.", IMEI);
                    CheckNumber();
                    CheckSms();
                }
            }

            public void CheckNumber()
            {
                try
                {
                    string currentNumber = module.GetNumber();

                    if (!PhoneNumber.Equals(currentNumber))
                    {
                        Console.WriteLine("[{0}][Info] Phone number from \"{1}\" changed to \"{2}\"", IMEI, PhoneNumber, currentNumber);
                        PhoneNumber = currentNumber;

                        dynamic phonenum = new JObject();

                        phonenum.number = currentNumber;
                        phonenum.imei = IMEI;

                        string registerPhoneJson = JsonConvert.SerializeObject(phonenum);

                        RegisterPhone(registerPhoneJson);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("[{0}][Error] Failed to read phone number: {1}", IMEI, ex.Message);
                }
            }

            public void CheckSms()
            {
                List<GsmCommunication.SMS> newsms = module.ReadSMS();

                if (newsms.Count > 0)
                {
                    Console.WriteLine("[{0}] Module trying to read latest sms messages...", IMEI);

                    foreach (var message in newsms)
                    {
                        Console.WriteLine("[{0}][SMS][{1}] From: {2}", IMEI, message.Date, message.Sender);

                        string messageHash = Functions.MD5(message.Date + message.Sender + message.ID + IMEI);

                        string outJson = CreateSMSJson(message, messageHash);

                        SendSMS(outJson);

                        Console.WriteLine("[{0}][SMS][JSON] {1}", IMEI, outJson);
                    }

                    if (newsms.Count > 15)
                    {
                        module.ClearSMS();
                    }
                }
            }

            private string CreateSMSJson(GsmCommunication.SMS message, string hash)
            {
                dynamic sms = new JObject();

                if (message == null) return sms;

                try
                {  
                    sms.fromId = message.Sender;
                    sms.deviceId = IMEI;
                    sms.bodyTxt = message.Message;
                    sms.phoneNumber = PhoneNumber;
                    sms.hash = hash;
                }
                catch(Exception ex)
                {
                    Console.WriteLine("[{0}][Error] Failed to create json from sms object: {1}", IMEI, ex.Message);
                }

                return JsonConvert.SerializeObject(sms);
            }

            public void SendSMS(string json)
            {
                try
                {
                    using (HttpRequest req = new HttpRequest())
                    {
                        req.UserAgent = "Mozilla/5.0 (Linux; Android X; GSMSender) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.93 Safari/537.36";
                        req.IgnoreProtocolErrors = true;
                        req.KeepAlive = true;
                        req.AddHeader("key", key);
                        req.Post(url, json, "application/json; charset=utf-8");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("[{0}][Error] Failed to send sms to server: {1}", IMEI, ex.Message);
                }
            }

            public void RegisterPhone(string json)
            {
                try
                {
                    using (HttpRequest req = new HttpRequest())
                    {
                        req.UserAgent = "Mozilla/5.0 (Linux; Android X; GSMSender) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.93 Safari/537.36";
                        req.IgnoreProtocolErrors = true;
                        req.KeepAlive = true;
                        req.AddHeader("key", key);
                        req.Post(url + "newnumber.php", json, "application/json; charset=utf-8");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}][Error] Failed to register phone number with server: {1}", IMEI, ex.Message);
                }
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
