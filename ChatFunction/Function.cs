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
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using LambdaSharp;
using LambdaSharp.ApiGateway;
using Demo.WebSocketsChat.Common;
using Demo.WebSocketsChat.Common.Records;
using Demo.WebSocketsChat.Common.DynamoDB;
using System.Runtime.CompilerServices;

namespace Demo.WebSocketsChat.ChatFunction {

    public class Function : ALambdaApiGatewayFunction {

        //--- Fields ---
        private DynamoTable _table;
        private string _notifyQueueUrl;
        private IAmazonSQS _sqsClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var connectionsTableName = config.ReadDynamoDBTableName("ConnectionsTable");
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");

            // initialize AWS clients
            _table = new DynamoTable(connectionsTableName, new AmazonDynamoDBClient());
            _sqsClient = new AmazonSQSClient();
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request, string username = null) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");

            // check if a user already exists or create a new one
            UserRecord user = null;
            if(username != null) {
                user = await _table.GetAsync(new UserRecord {
                    UserId = username
                });
            } else {
                username = $"Anonymous-{DynamoTable.GetRandomString(6)}";
            }

            // check if a new user is joining
            if(user == null) {

                // create user record
                user = new UserRecord {
                    UserId = username,
                    UserName = username
                };
                await _table.CreateAsync(user);

                // create user subscription to "General" channel
                await _table.CreateAsync(new SubscriptionRecord {
                    ChannelId = "General",
                    UserId = user.UserId
                });
            }

            // create connection record
            await _table.CreateAsync(new ConnectionRecord {
                UserId = user.UserId,
                ConnectionId = request.RequestContext.ConnectionId
            });

            // fetch all channels the user is subscribed to
            var subscriptions = await _table.GetAllSecondaryRecordsAsync<UserRecord, SubscriptionRecord>(user);
            foreach(var subscription in subscriptions) {

                // fetch all messages the user may have missed since last time they were connected
                var messages = await subscription.GetNewMessagesAsync(_table);
                foreach(var message in messages) {

                    // TODO: do a batch get on the users

                    // notify user about missed messages
                    var fromUser = _table.GetAsync(new UserRecord {
                        UserId = message.UserId
                    });
                    await NotifyUserAsync(user, fromUser, message);
                }
            }
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);

            // delete closed connection record
            await _table.DeleteAsync(new ConnectionRecord {
                ConnectionId = CurrentRequest.RequestContext.ConnectionId
            });

            // notify all connections about user who left
            if(user != null) {
                await NotifyAllAsync("#host", $"{user.UserName} left");
            }
        }

        public async Task SendMessageAsync(SendMessageRequest request) {

            // NOTE: this method is invoked by messages with the "send" route key

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                return;
            }

            // notify all connections about new message
            await NotifyAllAsync(user.UserName, request.Text);
        }

        public async Task RenameUserAsync(RenameUserRequest request) {

            // NOTE: this method is invoked by messages with the "rename" route key

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                return;
            }

            // validate requested username
            var username = request.UserName?.Trim() ?? "";
            if(
                string.IsNullOrEmpty(username)
                || username.StartsWith("#", StringComparison.Ordinal)
            ) {
                return;
            }

            // update user name
            var oldName = user.UserName;
            user.UserName = request.UserName;
            await _table.UpdateAsync(user);

            // notify all connections about renamed user
            await NotifyAllAsync("#host", $"{oldName} is now known as {user.UserName}");
        }

        private Task NotifyUserAsync(UserRecord user, UserRecord from, MessageRecord message)

            // TODO: missing code
            => throw new NotImplementedException();

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

        private async Task<UserRecord> GetUserFromConnectionId(string connectionId, [CallerMemberName] string caller = "") {

            // fetch the connection record associated with this connection ID
            var connection = await _table.GetAsync(new ConnectionRecord {
                ConnectionId = connectionId
            });
            if(connection == null) {
                LogWarn($"[{caller}] Could not load connection record (connection id: {{0}}, request id: {{}})", CurrentRequest.RequestContext.ConnectionId, CurrentRequest.RequestContext.RequestId);
                return null;
            }

            // fetch the user record associated with this connection
            var user = await _table.GetAsync(new UserRecord {
                UserId = connection.UserId
            });
            if(user == null) {
                LogWarn($"[{caller}] Could not load user record (userid: {{0}}, request id: {{}})", connection.UserId, CurrentRequest.RequestContext.RequestId);
                return null;
            }
            return user;
        }
    }
}
