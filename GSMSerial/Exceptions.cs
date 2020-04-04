using System;
using System.Collections.Generic;
using System.Text;

namespace SMSPDULib
{
	class UnknownSMSTypeException : Exception
	{
		public UnknownSMSTypeException(byte pduType) : 
			base(string.Format("Unknow SMS type. PDU type binary: {0}.", Convert.ToString(pduType, 2))) { }
	}
}
