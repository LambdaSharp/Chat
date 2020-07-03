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
using Demo.WebSocketsChat.Common.DataStore;

namespace Demo.WebSocketsChat.NotifyFunction {

    public sealed class Function : ALambdaQueueFunction<BroadcastMessage> {

        //--- Fields ---
        private IAmazonApiGatewayManagementApi _amaClient;
        private DataTable _table;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var connectionsTableName = config.ReadDynamoDBTableName("ConnectionsTable");
            var webSocketUrl = config.ReadText("Module::WebSocket::Url");

            // initialize AWS clients
            _amaClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
                ServiceURL = webSocketUrl
            });
            _table = new DataTable(connectionsTableName, new AmazonDynamoDBClient());
        }

        public override async Task ProcessMessageAsync(BroadcastMessage message) {

            // prepare message by serializing it
            var messageBytes = Encoding.UTF8.GetBytes(message.Payload);
            if(message.UserId != null) {

                // send message to user on all connections
                var connections = await _table.GetUserConnectionsAsync(message.UserId);
                await Task.WhenAll(connections.Select(connection => SendMessageToConnection(messageBytes, connection.ConnectionId)));
            } else if(message.ChannelId != null) {

                // send message to all users on the channel
                var subscriptions = await _table.GetChannelSubscriptionsAsync(message.ChannelId);
                await Task.WhenAll(subscriptions.Select(async subscription => {
                    var connections = await _table.GetUserConnectionsAsync(message.UserId);
                    await Task.WhenAll(connections.Select(connection => SendMessageToConnection(messageBytes, connection.ConnectionId)));
                }));
            } else {

                // send message to all users
                var users = await _table.GetAllUserAsync();
                await Task.WhenAll(users.Select(async user => {
                    var connections = await _table.GetUserConnectionsAsync(message.UserId);
                    await Task.WhenAll(connections.Select(connection => SendMessageToConnection(messageBytes, connection.ConnectionId)));
                }));
            }
        }

        private async Task SendMessageToConnection(byte[] messageBytes, string connectionId) {

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
            } catch(Exception e) {
                LogErrorAsWarning(e, "PostToConnectionAsync() failed on connection {0}", connectionId);
            }
        }
    }
}
