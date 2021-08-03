using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace webSocketTestConsole
{
    class Program
    {
        static void Main(string[] args)
        {

            Uri uri = new Uri("ws://localhost:8080/ws/websocket");
            WebSocketStomp webSocketStomp = new WebSocketStomp(uri);

            webSocketStomp.onMessage((header, data) => {
                Console.WriteLine(data);
            });

            webSocketStomp.subscribSync("/topic/request/scaleData");

            var data = new JObject();
            data.Add("command", "SEND");
            data.Add("message", "I love JH!!!");
            var dataText = JsonConvert.SerializeObject(data);

            webSocketStomp.sendSync("/request/scaleData", dataText);

            Console.ReadLine();
        }

        
    }

    public class WebSocketStomp
    {
        #region ★ 멤버 변수
        private Guid uuid = Guid.NewGuid();
        private ClientWebSocket ws = new ClientWebSocket();
        // task 취소 토큰
        private CancellationTokenSource cts = new CancellationTokenSource();

        public int timeout = 5000;
        #endregion

        #region ★ 생성자 & 소멸자
        public WebSocketStomp(Uri uri)
        {
            connectSync(uri);
        }

        ~WebSocketStomp()
        {
            cts.Cancel();
            cts.Dispose();
            ws.Dispose();
        }
        #endregion

        #region ★ 동기 실행
        private void connectSync(Uri uri)
        {
            ws.ConnectAsync(uri, cts.Token).Wait(timeout);
            
            var buf = new StringBuilder();

            buf.Append("CONNECT\n");
            buf.Append("content-length:0\n");
            buf.Append("accept-version:1.2\n");
            buf.Append("host:edge\n");
            buf.Append("\n");
            buf.Append("\0");

            ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
        }

        public void subscribSync(string destination)
        {
            var buf = new StringBuilder();

            buf.Append("SUBSCRIBE\n");
            buf.Append("content-length:0\n");
            buf.Append($"id:sub-{uuid}\n");
            buf.Append($"destination:{destination}\n");
            buf.Append("\n");
            buf.Append("\0");

            ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
        }

        public void sendSync(string destination, string message)
        {
            var buf = new StringBuilder();

            buf.Append("SEND\n");
            buf.Append($"content-length:{message.Length}\n");
            buf.Append($"destination:{destination}\n");
            buf.Append("\n");
            buf.Append(message);
            buf.Append("\0");

            ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
        }
        #endregion

        #region ★ 비동기 실행
        public void connectAsync(Uri uri)
        {
            Task.Run(() => {
                connectSync(uri);
            });
        }

        public void subscribAsync(string destination)
        {
            Task.Run(() => {
                subscribSync(destination);
            });
        }

        public void sendAsync(string destination, string message)
        {
            Task.Run(() => {
                sendSync(destination, message);
            });
        }
        #endregion

        #region ★ 이벤트
        public async void onMessage(Action<string, string> callback)
        {
            string result = "";
            byte[] receiveMsg = new byte[1];

            while (true)
            {
                await ws.ReceiveAsync(receiveMsg, cts.Token);
                var character = Encoding.Default.GetString(receiveMsg);

                result += character;

                if (character == "\0")
                {
                    var splitResult = result.Split('\n');
                    var data = splitResult[splitResult.Length - 1];
                    var header = "";
                    for (var i = 0; i < splitResult.Length - 1; i++)
                        header += $"{splitResult[i]}\n";

                    callback(header, data);
                    result = "";
                }
            }
        }
        #endregion
    }
}
