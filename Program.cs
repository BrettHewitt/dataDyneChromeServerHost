using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HostClientCommunication;
using NativeMessaging;
using Newtonsoft.Json.Linq;

namespace dataDyneChromeServerHost
{
    class Program
    {
        private static ChromeServerHost Host;
        
        private static bool ChromeConnectionStable = true;
        private static NamedPipeServerStream PipeServer;

        static void Main(string[] args)
        {
            Host = new ChromeServerHost();
            Host.LostConnectionToChrome += Host_LostConnectionToChrome;
            Thread serverThread = new Thread(ServerThread);
            serverThread.Start();
            Thread.Sleep(250);
                
            Host.Listen();
        }

        private static void Host_LostConnectionToChrome(object sender, EventArgs e)
        {
            ChromeConnectionStable = false;
            ResetEvent.Set();
        }

        static void ProcessConnection(IAsyncResult result)
        {
            try
            {
                PipeServer.EndWaitForConnection(result);

                // Read the request from the client. Once the client has
                // written to the pipe its security token will be available.

                ServerCommunication serverCommunication = new ServerCommunication(PipeServer);

                // Verify our identity to the connected client using a
                // string that the client anticipates.

                serverCommunication.SendMessage("dataDyne Chrome Server");

                string command = serverCommunication.ReadMessage();
                var j = new ResponseConfirmation(command);

                bool waitForReponse = true;
                bool processResponse = true;
                JObject reply = null;
                Host.MessageReceived += (s, a) =>
                {
                    //Handle Response
                    if (processResponse)
                    {
                        reply = a.Data;
                    }

                    waitForReponse = false;
                };
                Host.SendMessage(j.GetJObject());

                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (waitForReponse)
                {
                    if (sw.ElapsedMilliseconds == (10 * 1000))
                    {
                        //We've timed out
                        waitForReponse = false;
                        processResponse = false;
                    }
                }

                string response = "";
                if (processResponse && reply != null)
                {
                    //Send reply back
                    response = reply.ToString();
                }
                else
                {
                    //Reponse failed
                    response = "{\"text\": \"Failed to communicate with chrome extension\"}";
                }

                PipeServer.RunAsClient(() => serverCommunication.SendMessage(response));
            }
            catch (IOException e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }
            
            PipeServer.Close();

            ProcessFinishedEvent.Set();
        }
        
        private static ManualResetEvent ResetEvent;
        private static ManualResetEvent ProcessFinishedEvent;

        private static void ServerThread(object data)
        {
            ResetEvent = new ManualResetEvent(false);
            
            while (true)
            {
                PipeServer = new NamedPipeServerStream("dataDyneChromeServerPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                ProcessFinishedEvent = new ManualResetEvent(false);
                // Wait for a client to connect
                PipeServer.BeginWaitForConnection(ProcessConnection, PipeServer);
                
                int result = WaitHandle.WaitAny(new[] { ResetEvent, ProcessFinishedEvent });

                if (!ChromeConnectionStable || result == 0)
                {
                    PipeServer.Close();
                    break;
                }
            }
        }
    }
}
