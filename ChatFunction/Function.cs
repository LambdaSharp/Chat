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
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using LambdaSharp;
using LambdaSharp.ApiGateway;
using Demo.WebSocketsChat.Common;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Demo.WebSocketsChat.ChatFunction {

    public class Function : ALambdaApiGatewayFunction {

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        //--- Class Methods ---
        public static string RandomString(int length)
            => new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(chars => chars[_random.Next(chars.Length)]).ToArray());

        //--- Fields ---
        private ConnectionsTable _table;
        private string _notifyQueueUrl;
        private IAmazonSQS _sqsClient;

        //--- Properties ---
        public ConnectionUser CurrentUser { get; set; }

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _table = new ConnectionsTable(
                config.ReadDynamoDBTableName("ConnectionsTable"),
                new AmazonDynamoDBClient()
            );
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");
            _sqsClient = new AmazonSQSClient();
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request, string username = null) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");
            CurrentUser = new ConnectionUser {
                ConnectionId = request.RequestContext.ConnectionId,
                UserName = username ?? $"Anonymous-{RandomString(6)}"
            };
            await _table.PutRowAsync(CurrentUser);
            await NotifyAllAsync("#host", $"{CurrentUser.UserName} joined");
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");
            CurrentUser = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);
            if(CurrentUser != null) {
                await _table.DeleteRowAsync(CurrentUser.ConnectionId);
                await NotifyAllAsync("#host", $"{CurrentUser.UserName} left");
            }
        }

        public async Task SendMessageAsync(SendMessageRequest request) {
            CurrentUser = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);
            await NotifyAllAsync(CurrentUser.UserName, request.Text);
        }

        private async Task NotifyAllAsync(string username, string message) {
            await _sqsClient.SendMessageAsync(new Amazon.SQS.Model.SendMessageRequest {
                MessageBody = LambdaSerializer.Serialize(new NotifyMessage {
                    Message = LambdaSerializer.Serialize(new UserMessageResponse {
                        From = username,
                        Text = message
                    })
                }),
                QueueUrl = _notifyQueueUrl
            });
        }
    }
}
