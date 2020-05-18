/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2019
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
using Amazon.Lambda.Core;
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
            _amaClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
                ServiceURL = config.ReadText("Module::WebSocket::Url")
            });
            _table = new ConnectionsTable(
                config.ReadDynamoDBTableName("ConnectionsTable"),
                new AmazonDynamoDBClient()
            );
        }

        public override async Task ProcessMessageAsync(NotifyMessage message) {

            // enumerate open connections
            var connections = await _table.GetAllRowsAsync();
            LogInfo($"Announcing to {connections.Count()} open connection(s)");

            // attempt to send message on all open connections
            var messageBytes = Encoding.UTF8.GetBytes(message.Message);
            var outcomes = await Task.WhenAll(connections.Select(async (connectionId, index) => {
                LogInfo($"Post to connection {index}: {connectionId}");
                try {
                    await _amaClient.PostToConnectionAsync(new PostToConnectionRequest {
                        ConnectionId = connectionId,
                        Data = new MemoryStream(messageBytes)
                    });
                } catch(AmazonServiceException e) when(e.StatusCode == System.Net.HttpStatusCode.Gone) {
                    LogInfo($"Deleting gone connection: {connectionId}");
                    await _table.DeleteRowAsync(connectionId);
                    return false;
                } catch(Exception e) {
                    LogErrorAsWarning(e, "PostToConnectionAsync() failed");
                    return false;
                }
                return true;
            }));
            LogInfo($"Data sent to {outcomes.Count(result => result)} connections");
        }
    }
}
