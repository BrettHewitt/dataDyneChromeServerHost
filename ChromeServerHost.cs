using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NativeMessaging;
using Newtonsoft.Json.Linq;

namespace dataDyneChromeServerHost
{
    public class MessageReceievedArgs : EventArgs
    {
        public JObject Data
        {
            get;
            set;
        }
    }

    public delegate void MessageReceivedHandler(object sender, MessageReceievedArgs args);

    public class ChromeServerHost : Host
    {
        public event MessageReceivedHandler MessageReceived;

        public override string Hostname
        {
            get { return "com.datadyne.chromeserver.message"; }
        }

        public ChromeServerHost()
        {

        }

        protected override void ProcessReceivedMessage(JObject data)
        {
            if (MessageReceived != null)
            {
                MessageReceived(this, new MessageReceievedArgs() {Data = data});
            }
        }
    }
}
