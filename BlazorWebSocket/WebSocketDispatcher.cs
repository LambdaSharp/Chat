/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2020
 * lambdasharp.net
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorWebSocket {

    public class WebSocketDispatcher : IDisposable {

        //--- Class Methods ---
        private static bool TryParseJsonDocument(byte[] bytes, out JsonDocument document) {
            var utf8Reader = new Utf8JsonReader(bytes);
            return JsonDocument.TryParseValue(ref utf8Reader, out document);
        }

        //--- Fields ---
        private readonly Uri _serverUri;
        private readonly ClientWebSocket _webSocket = new ClientWebSocket();
        private readonly CancellationTokenSource _disposalTokenSource = new CancellationTokenSource();
        private readonly MemoryStream _messageAccumulator = new MemoryStream();
        private readonly Dictionary<string, Func<string, Task>> _actions = new Dictionary<string, Func<string, Task>>();

        //--- Constructors ---
        public WebSocketDispatcher(Uri serverUri) => _serverUri = serverUri ?? throw new ArgumentNullException(nameof(serverUri));

        //--- Properties ---
        public string IdToken { get; set; }
        public WebSocketState State => _webSocket.State;

        //--- Methods ---
        public async Task<bool> Connect() {

            // attempt to connect to server
            try {
                await ReconnectWebSocketAsync();
            } catch(WebSocketException e) {
                return false;
            }
            _ = ReceiveLoop();
            return true;
        }

        public void RegisterAction<T>(string action, Action<T> callback)
            => _actions.Add(action, json => {
                callback(JsonSerializer.Deserialize<T>(json));
                return Task.CompletedTask;
            });

        public void RegisterAction<T>(string action, Func<T, Task> callback)
            => _actions.Add(action, json => callback(JsonSerializer.Deserialize<T>(json)));

        private async Task ReconnectWebSocketAsync() {

            // build effective server uri
            var effectiveServerUri = string.IsNullOrEmpty(IdToken)
                ? _serverUri
                : (_serverUri.Query == "")
                ? new Uri($"{_serverUri}?id_token={IdToken}")
                : new Uri($"{_serverUri}&id_token={IdToken}");
            if(!string.IsNullOrEmpty(IdToken))                 {
                Console.WriteLine($"Using authorization token: {IdToken}");
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {IdToken}");
            } else {
                Console.WriteLine("No authorization token provided");
            }
            Console.WriteLine($"Connecting to: {effectiveServerUri}");

            // connect/reconnect to websocket
            await _webSocket.ConnectAsync(effectiveServerUri, _disposalTokenSource.Token);
            Console.WriteLine("Connected!");
        }

        public Task SendMessageAsync<T>(T message) => SendMessageAsync(JsonSerializer.Serialize(message));

        public async Task SendMessageAsync(string json) {
            Console.WriteLine($"Sending: {json}");
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, _disposalTokenSource.Token);
        }

        private async Task ReceiveLoop() {
            var buffer = new ArraySegment<byte>(new byte[32 * 1024]);
            while(!_disposalTokenSource.IsCancellationRequested) {
                var received = await _webSocket.ReceiveAsync(buffer, _disposalTokenSource.Token);
                switch(received.MessageType) {
                case WebSocketMessageType.Close:

                    // websocket connection is automatically closed when idle for 10 minutes or
                    // when it has been used for 2 hours
                    if(!_disposalTokenSource.IsCancellationRequested) {

                        // re-open connection while the app is still running
                        await ReconnectWebSocketAsync();
                    }
                    break;
                case WebSocketMessageType.Binary:

                    // unsupported message type; ignore it
                    break;
                case WebSocketMessageType.Text:

                    // text message payload may require more than one frame to be received fully
                    _messageAccumulator.Write(buffer.Array, 0, received.Count);

                    // check if all bytes of the message have been received
                    if(received.EndOfMessage) {

                        // convert accumulated messages into JSON string
                        var bytes = _messageAccumulator.ToArray();
                        var message = Encoding.UTF8.GetString(bytes);
                        _messageAccumulator.Position = 0;
                        _messageAccumulator.SetLength(0);

                        // deserialize into a generic JSON document
                        if(!TryParseJsonDocument(bytes, out var response)) {
                            Console.WriteLine($"Unabled to parse message as JSON document: {message}");
                        } else if(!response.RootElement.TryGetProperty("Action", out var action)) {
                            Console.WriteLine($"Missing 'Action' property in message: {message}");
                        } else if(action.ValueKind != JsonValueKind.String) {
                            Console.WriteLine($"Wrong type for 'Action' property in message: {message}");
                        } else if(!_actions.TryGetValue(action.GetString(), out var callbackAsync)) {
                            Console.WriteLine($"No registered callback for action '{action.GetString()}': {message}");
                        } else {
                            try {
                                await callbackAsync(message);
                            } catch(Exception e) {
                                Console.WriteLine($"Exception during callback for message: {message}\n{e}");
                            }
                        }
                    }
                    break;
                }
            }
        }

        public void Dispose() {
            _disposalTokenSource.Cancel();
            _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
    }
}
