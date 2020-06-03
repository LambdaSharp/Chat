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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using LambdaSharp;
using Demo.WebSocketsChat.Common;
using LambdaSharp.SimpleQueueService;

namespace Demo.WebSocketsChat.NotifyFunction {

    public class Function : ALambdaQueueFunction<NotifyMessage> {

        //--- Fields ---
        private IAmazonApiGatewayManagementApi _amaClient;
        private ConnectionsTable _table;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var connectionsTableName = config.ReadDynamoDBTableName("ConnectionsTable");
            var webSocketUrl = config.ReadText("Module::WebSocket::Url");

            // initialize AWS clients
            _amaClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
                ServiceURL = webSocketUrl
            });
            _table = new ConnectionsTable(
                connectionsTableName,
                new AmazonDynamoDBClient()
            );
        }

        public override async Task ProcessMessageAsync(NotifyMessage message) {

            // prepare message by serializing it
            var messageBytes = Encoding.UTF8.GetBytes(message.Message);

            // determine if message should be sent to one connection or all connections
            if(message.ConnectionId != null) {
                await SendMessageToConnection(messageBytes, message.ConnectionId);
            } else {

                // enumerate open connections
                var connections = await _table.GetAllRowsAsync();
                LogInfo($"Found {connections.Count():N0} open connection(s)");

                // attempt to send message on all open connections simultaneously
                var outcomes = await Task.WhenAll(
                    connections.Select(connectionId => SendMessageToConnection(messageBytes, connectionId))
                );
                LogInfo($"Message sent to {outcomes.Count(outcome => outcome):N0} connections");
            }
        }

        private  async Task<bool> SendMessageToConnection(byte[] messageBytes, string connectionId) {

            // attempt to send serialized message to connection
            try {
                LogInfo($"Post to connection: {connectionId}");
                await _amaClient.PostToConnectionAsync(new PostToConnectionRequest {
                    ConnectionId = connectionId,
                    Data = new MemoryStream(messageBytes)
                });
            } catch(AmazonServiceException e) when(e.StatusCode == System.Net.HttpStatusCode.Gone) {

                // HTTP Gone status code indicates the connection has been closed
                // connection will be removed by ChatFunction when the websocket gets closed
                return false;
            } catch(Exception e) {
                LogErrorAsWarning(e, "PostToConnectionAsync() failed on connection {0}", connectionId);
                return false;
            }
            return true;
        }
    }
}
