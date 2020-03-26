using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.Text.RegularExpressions;

namespace GSMSerial
{
    public class GsmCommunication
    {
        private SerialPort port = null;
        private AutoResetEvent receiveNow;
        private string portName = "";
        private bool idleReceive = false;

        public GsmCommunication(string portName)
        {
            this.portName = portName;
            receiveNow  = new AutoResetEvent(false);
        }

        #region Communication
        public void OpenPort()
        {
            port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
            port.ReadTimeout = 300;
            port.WriteTimeout = 300;
            port.Encoding = Encoding.GetEncoding("iso-8859-1");
            port.Open();
            port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
            port.DtrEnable = true;
            port.RtsEnable = true;
        }


        /*
         * Activates receiving and parsing of data while no commands are being sent to GSM modem.
         * 
         */
        public void idleReceiveStart()
        {
            idleReceive = true;
        }

        public void idleReceiveStop()
        {
            idleReceive = false;
        }

        public bool CheckPort()
        {
            try
            {
                // Check connection
                ExecCommand("AT", 200, "No phone connected at " + portName + ".");
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void ClosePort()
        {
            if (port != null)
            {
                port.Close();
                port.DataReceived -= new SerialDataReceivedEventHandler(port_DataReceived);
            }
        }

        void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType == SerialData.Chars)
            {
                receiveNow.Set();

                if (idleReceive)
                {
                    try
                    {
                        string resp = ReadResponse(500);

                        if (Regex.IsMatch(resp, "\\+CMTI: \"(.+)\",(\\d+)"))
                        {
                            string msg = Regex.Match(resp, "\\+CMTI: \"(.+)\",(\\d+)").Groups[1].Value;
                            string Id = Regex.Match(resp, "\\+CMTI: \"(.+)\",(\\d+)").Groups[2].Value;

                            //TODO: some logic to read last message
                            Console.WriteLine("[{0}] SMS message received. Message ID = {1}", portName, Id);
                        }
                    }
                    catch (ApplicationException ex)
                    {
                        Console.WriteLine("[{1}] reading failed, could be due to checks : {0}", ex.Message, portName);
                    }
                }
             }
        }

        private string ReadResponse(int timeout)
        {
            string buffer = string.Empty;
            do
            {
                if (receiveNow.WaitOne(timeout, false))
                {
                    string t = port.ReadExisting();
                    buffer += t;
                }
                else
                {
                    if (buffer.Length > 0)
                        throw new ApplicationException("Response received is incomplete.");
                    else
                        throw new ApplicationException("No data received from phone.");
                }
            }
            while (!buffer.EndsWith("\r\nOK\r\n") && !buffer.EndsWith("\r\nERROR\r\n") && !Regex.IsMatch(buffer, "\\+CMTI: \"(.+)\",(\\d+)"));
            return buffer;
        }

        #region Commands
        public string GetNumber()
        {
            if(port != null)
            {
                string resp = ExecCommand("AT+CNUM", 500, "Failed to get SIM card number on port " + portName);

                if (Regex.IsMatch(resp, "\\+CNUM: \"(.+)\",\"(.+)\",(\\d+),(\\d+),(\\d+)"))
                {
                    string number = Regex.Match(resp, "\\+CNUM: \"(.+)\",\"(.+)\",(\\d+),(\\d+),(\\d+)").Groups[2].Value;

                    return number;
                }
            }

            return null;
        }

        public string GetImei()
        {
            if (port != null)
            {
                string resp = ExecCommand("AT+CGSN", 500, "Failed to get IMEI on port " + portName);

                string imei = Regex.Match(resp, "(\\d{15})").Groups[1].Value;

                return imei;
            }

            return null;
        }
        #endregion

        private string ExecCommand(string command, int responseTimeout, string errorMessage)
        {
            try
            {
                port.DiscardOutBuffer();
                port.DiscardInBuffer();
                receiveNow.Reset();
                port.Write(command + "\r");

                string input = ReadResponse(responseTimeout);
                if ((input.Length == 0) || (!input.EndsWith("\r\nOK\r\n")))
                    throw new ApplicationException("No success message was received.");
                return input;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(errorMessage, ex);
            }
        }
        #endregion
    }
}
