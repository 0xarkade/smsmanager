using System;
using System.Collections.Generic;
using System.Text;
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

        public GsmCommunication(string portName)
        {
            this.portName = portName;
            receiveNow    = new AutoResetEvent(false);
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
            }
        }

        public class SMS
        {
            public int ID;
            public string Sender;
            public string Message;
            public string Date;
        }

        public List<SMS> ReadSMS()
        {
            List<SMS> messages = new List<SMS>();

            if (port != null)
            {
                try
                {
                    //Enable SMS Text Mode
                    ExecCommand("AT+CMGF=1", 300, "Failed to set Text message mode on port : " + portName);

                    //Set encoding to iso-8859-1
                    ExecCommand("AT+CSCS=\"8859-1\"", 500, "Failed to set message encoding on port: " + portName);

                    //Read Messages
                    string response = ExecCommand("AT+CMGL=\"ALL\"", 5000, "Failed to read SMS messages on port : " + portName);

                    Regex r = new Regex(@"\+CMGL: (\d+),""(.+)"",""(.+)"",(.*),""(.+)""\r\n(.+)\r\n");
                    Match m = r.Match(response);
                    while (m.Success)
                    {
                        SMS msg = new SMS();
                        msg.ID = int.Parse(m.Groups[1].Value);
                        msg.Sender = m.Groups[3].Value;
                        msg.Date   = m.Groups[5].Value;
                        msg.Message = m.Groups[6].Value;
                        messages.Add(msg);

                        m = m.NextMatch();
                    }

                    return messages;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}][Error] : {1}", portName, ex.Message);
                }
            }

            return messages;
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
            while (!buffer.EndsWith("\r\nOK\r\n") && !buffer.EndsWith("\r\nERROR\r\n"));
            return buffer;
        }

        #region Commands
        public string GetNumber()
        {
            if(port != null)
            {
                string resp = ExecCommand("AT+CNUM", 500, "Failed to get SIM card number on port " + portName);

                if (Regex.IsMatch(resp, "\\+CNUM: (.+),\"(.+)\",(\\d+),(\\d+),(\\d+)"))
                {
                    string number = Regex.Match(resp, "\\+CNUM: (.+),\"(.+)\",(\\d+),(\\d+),(\\d+)").Groups[2].Value;

                    return number;
                }
            }

            return null;
        }

        public void ClearSMS()
        {
            if(port != null)
            {
                try
                {
                    ExecCommand("AT+CMGD=1,4", 500, "Unable to delete sms messages on port: " + portName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}][Error] failed to delete sms messages on storage: {1}", portName, ex.Message);
                }
            }
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
