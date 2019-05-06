/*
 * MindTouch λ#
 * Copyright (C) 2018-2019 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit mindtouch.com;
 * please review the licensing section.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using LambdaSharp;
using LambdaSharp.ApiGateway;
using LambdaSharp.Demo.WebSocketsChat.Common;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaSharp.Demo.WebSocketsChat.ChatFunction {

    public class Function : ALambdaApiGatewayFunction {

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        //--- Class Methods ---
        public static string RandomString(int length)
            => new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(chars => chars[_random.Next(chars.Length)]).ToArray());

        //--- Fields ---
        private ConnectionsTable _table;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _table = new ConnectionsTable(
                config.ReadDynamoDBTableName("ConnectionsTable"),
                new AmazonDynamoDBClient()
            );
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");
            var record = new ConnectionUser {
                ConnectionId = request.RequestContext.ConnectionId,
                UserName = $"Anonymous-{RandomString(6)}"
            };
            await NotifyAllAsync("#host", $"{record.UserName} joined");
            await _table.PutRowAsync(record);
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");
            var record = await _table.GetRowAsync<ConnectionUser>(request.RequestContext.ConnectionId);
            await _table.DeleteRowAsync(request.RequestContext.ConnectionId);
            if(record != null) {
                await NotifyAllAsync("#host", $"{record.UserName} left");
            }
        }

        public async Task SendMessageAsync(SendMessageRequest request) {
            var record = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);
            await NotifyAllAsync(record.UserName, request.Text);
        }

        private async Task NotifyAllAsync(string username, string message) {

            // enumerate open connections
            var connections = await _table.GetAllRowsAsync();
            LogInfo($"Announcing to {connections.Count()} open connection(s)");

            // attempt to send message on all open connections
            var messageBytes = Encoding.UTF8.GetBytes(SerializeJson(new UserMessageResponse {
                From = username,
                Text = message
            }));
            var outcomes = await Task.WhenAll(connections.Select(async (connectionId, index) => {
                LogInfo($"Post to connection {index}: {connectionId}");
                try {
                    if(!await SendMessageToWebSocketConnectionAsync(connectionId, messageBytes)) {
                        LogInfo($"Deleting gone connection: {connectionId}");
                        await _table.DeleteRowAsync(connectionId);
                        return false;
                    }
                } catch(Exception e) {
                    LogErrorAsWarning(e, "SendMessageToWebSocketConnectionAsync() failed");
                    return false;
                }
                return true;
            }));
            LogInfo($"Data sent to {outcomes.Count(result => result)} connections");
        }
    }
}
