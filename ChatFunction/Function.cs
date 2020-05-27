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
using Amazon.SQS;
using LambdaSharp;
using LambdaSharp.ApiGateway;
using Demo.WebSocketsChat.Common;

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

            // read configuration settings
            var connectionsTableName = config.ReadDynamoDBTableName("ConnectionsTable");
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");

            // initialize AWS clients
            _table = new ConnectionsTable(
                connectionsTableName,
                new AmazonDynamoDBClient()
            );
            _sqsClient = new AmazonSQSClient();
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request, string username = null) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");

            // create new connection record with custom or random name
            CurrentUser = new ConnectionUser {
                ConnectionId = request.RequestContext.ConnectionId,
                UserName = username ?? $"Anonymous-{RandomString(6)}"
            };
            await _table.PutRowAsync(CurrentUser);

            // notify new connection with assigned user name
            await NotifyUserNameAsync(request.RequestContext.ConnectionId, CurrentUser.UserName);

            // notify all connections about user who joined
            await NotifyAllAsync("#host", $"{CurrentUser.UserName} joined");
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");

            // fetch user record associated with this connection
            CurrentUser = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);
            if(CurrentUser != null) {

                // remove connection record
                await _table.DeleteRowAsync(CurrentUser.ConnectionId);

                // notify all connections about user who left
                await NotifyAllAsync("#host", $"{CurrentUser.UserName} left");
            }
        }

        public async Task SendMessageAsync(SendMessageRequest request) {

            // NOTE: this method is invoked by messages with the "send" route key

            // fetch user record associated with this connection
            CurrentUser = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);

            // notify all connections about new message
            await NotifyAllAsync(CurrentUser.UserName, request.Text);
        }

        public async Task RenameUserAsync(RenameUserRequest request) {

            // NOTE: this method is invoked by messages with the "rename" route key

            // validate requested username
            var username = request.UserName?.Trim() ?? "";
            if(
                string.IsNullOrEmpty(username)
                || username.StartsWith("#", StringComparison.Ordinal)
            ) {
                return;
            }

            // fetch user record associated with this connection
            CurrentUser = await _table.GetRowAsync<ConnectionUser>(CurrentRequest.RequestContext.ConnectionId);

            // update user name
            var oldName = CurrentUser.UserName;
            CurrentUser.UserName = request.UserName;
            await _table.PutRowAsync(CurrentUser);

            // notify all connections about renamed user
            await NotifyAllAsync("#host", $"{oldName} is now known as {CurrentUser.UserName}");
        }

        private Task NotifyAllAsync(string from, string message)
            => NotifyAsync(new UserMessageResponse {
                From = from,
                Text = message
            });

        private Task NotifyUserNameAsync(string connectionId, string userName)
            => NotifyAsync(new UserNameResponse {
                UserName = userName
            }, connectionId);

        private Task NotifyAsync<T>(T response, string connectionId = null) where T : NotifyResponse
            => _sqsClient.SendMessageAsync(new Amazon.SQS.Model.SendMessageRequest {
                MessageBody = LambdaSerializer.Serialize(new NotifyMessage {
                    ConnectionId = connectionId,
                    Message = LambdaSerializer.Serialize(response)
                }),
                QueueUrl = _notifyQueueUrl
            });
    }
}
