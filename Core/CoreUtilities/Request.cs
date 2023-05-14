﻿using BepInEx.Logging;
using Newtonsoft.Json;
using SIT.Core.Coop;
using SIT.Core.Core.Web;
using SIT.Core.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SIT.Tarkov.Core
{
    public class Request : IDisposable
    {
        public const int DEFAULT_TIMEOUT_MS = 1000;
        public const int DEFAULT_TIMEOUT_LONG_MS = 9999;

        private string m_Session;

        public string Session
        {
            get
            {
                return m_Session;
            }
            set { m_Session = value; }
        }



        private string m_RemoteEndPoint;

        public string RemoteEndPoint
        {
            get
            {
                if (string.IsNullOrEmpty(m_RemoteEndPoint))
                    m_RemoteEndPoint = PatchConstants.GetBackendUrl();

                return m_RemoteEndPoint;

            }
            set { m_RemoteEndPoint = value; }
        }

        //public bool isUnity;
        private Dictionary<string, string> m_RequestHeaders { get; set; }

        private static Request m_Instance { get; set; }
        public static Request Instance
        {
            get
            {
                if (m_Instance == null || m_Instance.Session == null || m_Instance.RemoteEndPoint == null)
                    m_Instance = new Request();

                return m_Instance;
            }
        }

        public HttpClient HttpClient { get; set; }

        private ManualLogSource m_ManualLogSource;

        static WebSocketSharp.WebSocket WebSocket { get; set; }


        private Request(BepInEx.Logging.ManualLogSource logger = null)
        {
            // disable SSL encryption
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            if (logger != null)
                m_ManualLogSource = logger;
            else
                m_ManualLogSource = BepInEx.Logging.Logger.CreateLogSource("Request");

            if (string.IsNullOrEmpty(RemoteEndPoint))
                RemoteEndPoint = PatchConstants.GetBackendUrl();

            GetHeaders();
            PeriodicallySendPooledData();
            if (WebSocket == null)
            {
                m_ManualLogSource.LogDebug("Request Instance is connecting to WebSocket");
                var wsUrl = $"{PatchConstants.GetBackendUrl().Replace("http", "ws").Replace("6969","6970")}/{Session}?";
                m_ManualLogSource.LogDebug(wsUrl);
           
                WebSocket = new WebSocketSharp.WebSocket(wsUrl);
                WebSocket.OnError += WebSocket_OnError;
                WebSocket.OnMessage += WebSocket_OnMessage;
                WebSocket.Connect();
                WebSocket.Send("CONNECTED FROM SIT COOP");
            }



            HttpClient = new HttpClient();
            foreach (var item in GetHeaders())
            {
                HttpClient.DefaultRequestHeaders.Add(item.Key, item.Value);
            }
            HttpClient.MaxResponseContentBufferSize = long.MaxValue;
            HttpClient.Timeout = new TimeSpan(0, 0, 0, 0, 1000);
        }

        private void WebSocket_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            m_ManualLogSource.LogError($"WebSocket_OnError: {e.Message}");
        }

        private void WebSocket_OnMessage(object sender, WebSocketSharp.MessageEventArgs e)
        {
            //m_ManualLogSource.LogDebug("WebSocket_OnMessage");
            //m_ManualLogSource.LogDebug(e.Data);
            var packet = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
            if(CoopGameComponent.TryGetCoopGameComponent(out var coopGameComponent))
            {
                // This can cause Unity crash (different threads) but would be ideal >> FAST << sync logic! If it worked!
                //coopGameComponent.ReadFromServerLastActionsParseData(packet);

                coopGameComponent.ActionPackets.TryAdd(packet);

            }
        }

        public static Request GetRequestInstance(bool createInstance = false, BepInEx.Logging.ManualLogSource logger = null)
        {
            if (createInstance)
            {
                return new Request(logger);
            }

            return Request.Instance;
        }

        public BlockingCollection<KeyValuePair<string, Dictionary<string, object>>> PooledDictionariesToPost { get; } = new BlockingCollection<KeyValuePair<string, Dictionary<string, object>>>(new ConcurrentQueue<KeyValuePair<string, Dictionary<string, object>>>());
        ConcurrentQueue<KeyValuePair<string, string>> m_PooledStringToPost = new ConcurrentQueue<KeyValuePair<string, string>>();

        public void SendDataToPool(string url, Dictionary<string, object> data)
        {
            PooledDictionariesToPost.Add(new(url, data));
        }

        public void SendDataToPool(string url, string stringData)
        {
            m_PooledStringToPost.Enqueue(new(url, stringData));
        }

        public int PostPing { get; private set; }

        private Task PeriodicallySendPooledDataTask;

        private void PeriodicallySendPooledData()
        {
            //PatchConstants.Logger.LogDebug($"PeriodicallySendPooledData()");

            PeriodicallySendPooledDataTask = Task.Run(async () =>
            {
                int awaitPeriod = 1;
                //GCHelpers.EnableGC();
                //GCHelpers.ClearGarbage();
                //PatchConstants.Logger.LogDebug($"PeriodicallySendPooledData():In Async Task");

                //while (m_Instance != null)
                Stopwatch swPing = new Stopwatch();

                while (true)
                {
                    swPing.Restart();
                    await Task.Delay(awaitPeriod);
                    //await Task.Delay(100);
                    //while (PooledDictionariesToPost.Any())
                    {
                        KeyValuePair<string, Dictionary<string, object>> d;
                        if (PooledDictionariesToPost.TryTake(out d))
                        {
                            var url = d.Key;
                            var json = JsonConvert.SerializeObject(d.Value);
                            if(WebSocket != null)
                            {
                                if(WebSocket.ReadyState == WebSocketSharp.WebSocketState.Open)
                                {
                                    //PatchConstants.Logger.LogDebug($"WS:Periodic Send");
                                    WebSocket.Send(json);
                                }
                            }
                            //_ = await PostJsonAsync(url, json, timeout: 3000 + PostPing, debug: true);
                        }
                    }

                    //if (m_PooledStringToPost.Any())
                    //{
                    //    m_PooledStringToPost.TryDequeue(out var d);
                    //    var url = d.Key;
                    //    var json = d.Value;
                    //    await PostJsonAsync(url, json, timeout: 3000 + PostPing, debug: true);
                    //}
                    PostPing = (int)swPing.ElapsedMilliseconds - awaitPeriod;

                }
            });
        }

        private Dictionary<string, string> GetHeaders()
        {
            if(m_RequestHeaders != null && m_RequestHeaders.Count > 0)  
                return m_RequestHeaders;

            string[] args = Environment.GetCommandLineArgs();

            foreach (string arg in args)
            {
                if (arg.Contains("-token="))
                {
                    Session = arg.Replace("-token=", string.Empty);
                    m_RequestHeaders = new Dictionary<string, string>()
                        {
                            { "Cookie", $"PHPSESSID={Session}" },
                            { "SessionId", Session }
                        };
                    break;
                }
            }
            return m_RequestHeaders;
        }

        private async Task SendAndForgetAsync(string url, string method, string data, bool compress = true, int timeout = 1000, bool debug = false)
        {
            if (method == "GET")
            {
                throw new NotSupportedException("GET wont work on a SendAndForget call. It won't receive anything!");
            }

            if(data == null)
            {
                throw new ArgumentNullException("data", "data value must be provided");
            }

            // Force to DEBUG mode if not Compressing.
            debug = debug || !compress;

            method = method.ToUpper();

            var fullUri = url;
            if (!Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                fullUri = RemoteEndPoint + fullUri;

            var uri = new Uri(fullUri);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.ServerCertificateValidationCallback = delegate { return true; };

            foreach (var item in GetHeaders())
            {
                request.Headers.Add(item.Key, item.Value);
            }

            if (!debug && method == "POST")
            {
                request.Headers.Add("Accept-Encoding", "deflate");
            }

            request.Method = method;
            request.Timeout = timeout;

            if (debug && method == "POST")
            {
                compress = false;
                request.Headers.Add("debug", "1");
            }

            // set request body
            var inputDataBytes = Encoding.UTF8.GetBytes(data);
            byte[] bytes = (compress) ? Zlib.Compress(inputDataBytes, ZlibCompression.Fastest) : Encoding.UTF8.GetBytes(data);
            data = null;
            request.ContentType = "application/json";
            request.ContentLength = bytes.Length;
            if (compress)
                request.Headers.Add("content-encoding", "deflate");

            try
            {
                using (Stream stream = await request.GetRequestStreamAsync())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                await request.GetResponseAsync();
            }
            catch (Exception e)
            {
                PatchConstants.Logger.LogError(e);
            }
            finally
            {
                bytes = null;
                inputDataBytes = null;
            }
        }

        /// <summary>
        /// Send request to the server and get Stream of data back
        /// </summary>
        /// <param name="url">String url endpoint example: /start</param>
        /// <param name="method">POST or GET</param>
        /// <param name="data">string json data</param>
        /// <param name="compress">Should use compression gzip?</param>
        /// <returns>Stream or null</returns>
        private MemoryStream SendAndReceive(string url, string method = "GET", string data = null, bool compress = true, int timeout = 9999, bool debug = false)
        {
            // Force to DEBUG mode if not Compressing.
            debug = debug || !compress;

            HttpClient.Timeout = new TimeSpan(0, 0, 0, 0, timeout);

            method = method.ToUpper();

            var fullUri = url;
            if (!Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                fullUri = RemoteEndPoint + fullUri;

            if (method == "GET")
            {
                var ms = new MemoryStream();
                var stream = HttpClient.GetStreamAsync(fullUri);
                stream.Result.CopyTo(ms);
                return ms;
            }
            else if (method == "POST" || method == "PUT")
            {
                var uri = new Uri(fullUri);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.ServerCertificateValidationCallback = delegate { return true; };
             
                foreach (var item in GetHeaders())
                {
                    request.Headers.Add(item.Key, item.Value);
                }

                if (!debug && method == "POST")
                {
                    request.Headers.Add("Accept-Encoding", "deflate");
                }

                request.Method = method;
                request.Timeout = timeout;
                //request.Timeout = 60 * 100000;

                if (!string.IsNullOrEmpty(data))
                {
                    if (debug && method == "POST")
                    {
                        compress = false;
                        request.Headers.Add("debug", "1");
                    }

                    // set request body
                    var inputDataBytes = Encoding.UTF8.GetBytes(data);
                    byte[] bytes = (compress) ? Zlib.Compress(inputDataBytes, ZlibCompression.Fastest) : inputDataBytes;
                    data = null;
                    request.ContentType = "application/json";
                    request.ContentLength = bytes.Length;
                    if (compress)
                        request.Headers.Add("content-encoding", "deflate");

                    try
                    {
                        using (Stream stream = request.GetRequestStream())
                        {
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception e)
                    {
                        PatchConstants.Logger.LogError(e);
                    }
                    finally
                    {
                        bytes = null;
                        inputDataBytes = null;
                    }
                }

                // get response stream
                //WebResponse response = null;
                var ms = new MemoryStream();
                try
                {
                    using (var response = request.GetResponse())
                    {
                        using (var responseStream = response.GetResponseStream())
                            responseStream.CopyTo(ms);
                    }
                }
                catch (Exception e)
                {
                    PatchConstants.Logger.LogError(e);
                }
                finally
                {
                    fullUri = null;
                    request = null;
                    uri = null;
                }
                return ms;
            }

            throw new ArgumentException($"Unknown method {method}");

        }

        public byte[] GetData(string url, bool hasHost = false)
        {
            using (var dataStream = SendAndReceive(url, "GET"))
                return dataStream.ToArray();
        }

        public void PutJson(string url, string data, bool compress = true, int timeout = 1000, bool debug = false)
        {
            using (Stream stream = SendAndReceive(url, "PUT", data, compress, timeout, debug)) { }
        }

        public string GetJson(string url, bool compress = true, int timeout = 1000)
        {
            using (MemoryStream stream = SendAndReceive(url, "GET", null, compress, timeout))
            {
                if (stream == null)
                    return "";
                var bytes = stream.ToArray();
                var dec = Zlib.Decompress(bytes);
                var result = Encoding.UTF8.GetString(dec);
                dec = null;
                bytes = null;
                return result;
            }
        }

        public async Task<string> GetJsonAsync(string url, bool compress = true, int timeout = 1000)
        {
            try
            {
                var fullUri = url;
                if (!Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                    fullUri = RemoteEndPoint + fullUri;

                using (var ms = new MemoryStream())
                {
                    var stream = await HttpClient.GetStreamAsync(fullUri);
                    stream.CopyTo(ms);

                    var bytes = ms.ToArray();
                    var dec = Zlib.Decompress(bytes);
                    var result = Encoding.UTF8.GetString(dec);
                    dec = null;
                    bytes = null;
                    return result;
                }
            }
            catch (Exception ex)
            {
                PatchConstants.Logger.LogDebug(ex);
            }

            return null;
        }

        public string PostJson(string url, string data, bool compress = true, int timeout = 1000, bool debug = false)
        {
            using (MemoryStream stream = SendAndReceive(url, "POST", data, compress, timeout, debug))
            {
                if (stream == null)
                    return "";
                var bytes = stream.ToArray();
                byte[] resultBytes = null;
                if (compress)
                {
                    if (Zlib.IsCompressed(bytes))
                        resultBytes = Zlib.Decompress(bytes);
                    else
                        resultBytes = bytes;
                }
                else
                {
                    resultBytes = bytes;
                }
                var result = Encoding.UTF8.GetString(resultBytes);
                resultBytes = null;
                bytes = null;
                return result;
            }
        }

        public async Task<string> PostJsonAsync(string url, string data, bool compress = true, int timeout = DEFAULT_TIMEOUT_MS, bool debug = false)
        {
            return await Task.FromResult(PostJson(url, data, compress, timeout, debug));
        }

        public async void PostJsonAndForgetAsync(string url, string data, bool compress = true, int timeout = DEFAULT_TIMEOUT_LONG_MS, bool debug = false)
        {
            try
            {
                _ = await Task.Run(() => PostJson(url, data, compress, timeout, debug));
            }
            catch (Exception ex)
            { 
                PatchConstants.Logger.LogError(ex);
            }
        }


        /// <summary>
        /// Retrieves data asyncronously and parses to the desired type
        /// </summary>
        /// <typeparam name="T">Desired type to Deserialize to</typeparam>
        /// <param name="url">URL to call</param>
        /// <param name="data">data to send</param>
        /// <returns></returns>
        public async Task<T> PostJsonAsync<T>(string url, string data, int timeout = DEFAULT_TIMEOUT_MS)
        {
            var json = await PostJsonAsync(url, data, timeout: timeout);
            return await Task.FromResult(JsonConvert.DeserializeObject<T>(json));
        }

        //public Texture2D GetImage(string url, bool compress = true)
        //{
        //    using (Stream stream = SendAndReceive(url, "GET", null, compress))
        //    {
        //        using (MemoryStream ms = new MemoryStream())
        //        {
        //            if (stream == null)
        //                return null;
        //            Texture2D texture = new Texture2D(8, 8);

        //            stream.CopyTo(ms);
        //            texture.LoadImage(ms.ToArray());
        //            return texture;
        //        }
        //    }
        //}

        public void Dispose()
        {
            Session = null;
            RemoteEndPoint = null;
        }
    }
}
