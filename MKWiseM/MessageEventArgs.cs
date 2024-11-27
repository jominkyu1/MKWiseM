using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKWiseM
{
    public class MessageEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public string ErrorLog { get; private set; }
        public MessageEventArgs(string message, string errorLog = "")
        {
            this.Message = message;
            this.ErrorLog = errorLog;
        }
    }
}
