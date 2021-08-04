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
        reTry:
            try
            {
                var webSocketStomp = new WebSocketStomp("localhost:8080/ws");

                webSocketStomp.onMessage((header, data, err) =>
                {
                    if (err != null) throw err;
                    Console.WriteLine(header);
                    Console.WriteLine(data);
                });

                webSocketStomp.subscribeSync("/topic/data/1/particle");

                while (true)
                {
                    var data = new JObject();
                    data.Add("command", "SEND");
                    data.Add("message", "테스트!!");
                    var dataText = JsonConvert.SerializeObject(data);

                    webSocketStomp.sendSync("/data/1/particle", JsonConvert.SerializeObject(data));

                    Task.Delay(2000, CancellationToken.None).Wait();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());

                bool returnCheck = true;
                try { Task.Delay(1000, CancellationToken.None).Wait(); }
                catch (Exception) { returnCheck = false; }
                if (returnCheck) goto reTry;
            }

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
        public WebSocketStomp(string url)
        {
            connectSync(url);
        }

        ~WebSocketStomp()
        {
            dispose();
        }
        #endregion

        #region ★ 공통
        private void wrapingFunc(Action callback)
        {
            try
            {
                callback();
            }
            catch(Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public void dispose()
        {
            cts.Cancel();
            cts.Dispose();
            ws.Dispose();
        }
        #endregion

        #region ★ 동기 실행
        private void connectSync(string url)
        {
            wrapingFunc(() => {
                // 웹소켓 연결
                var uri = new Uri($"ws://{url}/websocket");
                var conResult = ws.ConnectAsync(uri, cts.Token).Wait(timeout);
                
                // 유효성 검사
                if (!conResult) throw new Exception("WebSocket connect error!!");

                // stomp 연결
                var buf = new StringBuilder();

                buf.Append("CONNECT\n");
                buf.Append("content-length:0\n");
                buf.Append("accept-version:1.2\n");
                buf.Append("host:edge\n");
                buf.Append("\n");
                buf.Append("\0");

                ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
            });
        }

        public void subscribeSync(string destination)
        {
            wrapingFunc(() => {
                var buf = new StringBuilder();

                buf.Append("SUBSCRIBE\n");
                buf.Append("content-length:0\n");
                buf.Append($"id:sub-{uuid}\n");
                buf.Append($"destination:{destination}\n");
                buf.Append("\n");
                buf.Append("\0");

                ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
            });
        }

        public void sendSync(string destination, string message)
        {
            wrapingFunc(()=> {
                var buf = new StringBuilder();

                buf.Append("SEND\n");
                buf.Append($"content-length:{Encoding.UTF8.GetByteCount(message)}\n");
                buf.Append($"destination:{destination}\n");
                buf.Append("\n");
                buf.Append(message);
                buf.Append("\0");

                ws.SendAsync(Encoding.UTF8.GetBytes(buf.ToString()), WebSocketMessageType.Text, true, cts.Token).Wait(timeout);
            });
        }
        #endregion

        #region ★ 비동기 실행
        public void connectAsync(string url)
        {
            Task.Run(() => {
                connectSync(url);
            });
        }

        public void subscribeAsync(string destination)
        {
            Task.Run(() => {
                subscribeSync(destination);
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
        public void onMessage(Action<string, string, Exception> callback)
        {
            Task.Run(async () => {
                try
                {
                    List<byte> result = new List<byte>();
                    byte[] receiveMsg = new byte[1];

                    while (true)
                    {
                        await ws.ReceiveAsync(receiveMsg, cts.Token);
                        result.Add(receiveMsg[0]);

                        var character = Encoding.Default.GetString(receiveMsg);
                        if (character == "\0")
                        {
                            var resultText = Encoding.Default.GetString(result.ToArray());

                            var splitResult = resultText.Split('\n');
                            var data = splitResult[splitResult.Length - 1];
                            var header = "";
                            for (var i = 0; i < splitResult.Length - 1; i++)
                                header += $"{splitResult[i]}\n";

                            callback(header, data, null);
                            result.Clear();
                        }
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception e)
                {
                    callback(null, null, e);
                }
            });
        }
        #endregion
    }
}
