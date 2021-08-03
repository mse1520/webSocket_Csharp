using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace webSocketTestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            ClientWebSocket ws = new ClientWebSocket();
            Uri uri = new Uri("ws://localhost:8080/ws/websocket");

            ws.ConnectAsync(uri, CancellationToken.None);
            Thread.Sleep(2000);

            Console.WriteLine(ws.State); // open

            receiveMsg(ws, (msg) => {
                Console.WriteLine(msg);
            });

            var connectBuffer = new StringBuilder();
            var subscribeBuffer = new StringBuilder();
            var sendBuffer = new StringBuilder();

            connectBuffer.Append("CONNECT\n");
            connectBuffer.Append("content-length:0\n");
            connectBuffer.Append("accept-version:1.2\n");
            connectBuffer.Append("host:edge\n");
            connectBuffer.Append("\n");
            connectBuffer.Append("\0");

            subscribeBuffer.Append("SUBSCRIBE\n");
            subscribeBuffer.Append("content-length:0\n");
            subscribeBuffer.Append("id:sub-0\n");
            subscribeBuffer.Append("destination:/topic/request/scaleData\n");
            subscribeBuffer.Append("\n");
            subscribeBuffer.Append("\0");

            sendBuffer.Append("SEND\n");
            sendBuffer.Append("content-length:52\n");
            sendBuffer.Append("id:sub-0\n");
            sendBuffer.Append("destination:/request/scaleData\n");
            sendBuffer.Append("\n");
            sendBuffer.Append("{\"Subject\":\"Stomp client\",\"Message\":\"Hello World!!\"}");
            sendBuffer.Append("\0");
            
            ws.SendAsync(Encoding.UTF8.GetBytes(connectBuffer.ToString()), WebSocketMessageType.Text, true, CancellationToken.None);
            Thread.Sleep(1000);
            ws.SendAsync(Encoding.UTF8.GetBytes(subscribeBuffer.ToString()), WebSocketMessageType.Text, true, CancellationToken.None);
            Thread.Sleep(1000);
            ws.SendAsync(Encoding.UTF8.GetBytes(sendBuffer.ToString()), WebSocketMessageType.Text, true, CancellationToken.None);
            Thread.Sleep(1000);

            

            //byte[] receiveMsg = new byte[1000];
            //ws.ReceiveAsync(receiveMsg, CancellationToken.None);
            //Thread.Sleep(1000);
            //Console.WriteLine(Encoding.Default.GetString(receiveMsg));
            //Console.WriteLine("receive end!!");
            //ws.ReceiveAsync(receiveMsg, CancellationToken.None);
            //Thread.Sleep(1000);
            //Console.WriteLine(Encoding.Default.GetString(receiveMsg));
            //Console.WriteLine("receive end!!");

            Console.ReadLine();
        }

        private static async void receiveMsg(ClientWebSocket ws, Action<string> callback)
        {
            string result = "";
            byte[] receiveMsg = new byte[1];

            while (true)
            {
                await ws.ReceiveAsync(receiveMsg, CancellationToken.None);
                var character = Encoding.Default.GetString(receiveMsg);

                result += character;

                if (character == "\0")
                {
                    callback(result);
                    result = "";
                }
            }
        }
    }
}
