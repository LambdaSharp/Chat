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
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using LambdaSharp;
using LambdaSharp.ApiGateway;
using Demo.WebSocketsChat.Common;
using Demo.WebSocketsChat.Common.Records;
using System.Runtime.CompilerServices;
using Demo.WebSocketsChat.Common.DataStore;

namespace Demo.WebSocketsChat.ChatFunction {

    public class Function : ALambdaApiGatewayFunction {

        //--- Fields ---
        private DataTable _table;
        private string _notifyQueueUrl;
        private IAmazonSQS _sqsClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var connectionsTableName = config.ReadDynamoDBTableName("ConnectionsTable");
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");

            // initialize AWS clients
            _sqsClient = new AmazonSQSClient();
            _table = new DataTable(connectionsTableName, new AmazonDynamoDBClient());
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request, string userName = null) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");

            // check if a user already exists or create a new one
            UserRecord user = null;
            if(userName != null) {
                user = await _table.GetUserAsync(userName);
            } else {
                userName = $"Anonymous-{DataTable.GetRandomString(6)}";
            }

            // check if a new user is joining
            if(user == null) {

                // create user record
                user = new UserRecord {
                    UserId = userName,
                    UserName = userName
                };
                await _table.CreateUserAsync(user);

                // create user subscription to "General" channel
                await _table.CreateSubscriptionAsync(new SubscriptionRecord {
                    ChannelId = "General",
                    UserId = user.UserId
                });
            }

            // create connection record
            var connection = new ConnectionRecord {
                ConnectionId = request.RequestContext.ConnectionId,
                UserId = user.UserId
            };
            await _table.CreateConnectionAsync(connection);

            // fetch all channels the user is subscribed to
            var subscriptions = await _table.GetUserSubscriptionsAsync(user.UserId);
            foreach(var subscription in subscriptions) {

                // fetch all messages the user may have missed since last time they were connected
                var messages = await _table.GetChannelMessagesAsync(subscription.ChannelId, subscription.LastSeenTimestamp);
                foreach(var message in messages) {

                    // notify user about missed messages
                    await NotifyAsync(user.UserId, channelId: null, new UserMessageNotification {
                        UserId = message.UserId,
                        ChannelId = message.ChannelId,
                        Text = message.Message
                    });
                }
            }
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);

            // delete closed connection record
            await _table.DeleteConnectionAsync(CurrentRequest.RequestContext.ConnectionId, user.UserId);
        }

        public async Task SendMessageAsync(SendMessageRequest request) {

            // NOTE: this method is invoked by messages with the "send" route key

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                return;
            }

            // notify all connections about new message
            await NotifyAsync(userId: null, channelId: request.ChannelId, new UserMessageNotification {
                UserId = user.UserId,
                ChannelId = request.ChannelId,
                Text = request.Text
            });
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
            var oldUserName = user.UserName;
            user.UserName = request.UserName;
            await _table.UpdateUserAsync(user);

            // notify all connections about renamed user
            await NotifyAsync(userId: null, channelId: null, new UserNameNotification {
                UserId = user.UserId,
                UserName = user.UserName,
                OldUserName = oldUserName
            });
        }

        private Task NotifyAsync<T>(string userId, string channelId, T notification) where T : Notification
            => _sqsClient.SendMessageAsync(new Amazon.SQS.Model.SendMessageRequest {
                MessageBody = LambdaSerializer.Serialize(new BroadcastMessage {
                    UserId = userId,
                    ChannelId = channelId,
                    Payload = LambdaSerializer.Serialize(notification)
                }),
                QueueUrl = _notifyQueueUrl
            });

        private async Task<UserRecord> GetUserFromConnectionId(string connectionId, [CallerMemberName] string caller = "") {

            // fetch the connection record associated with this connection ID
            var connection = await _table.GetConnectionAsync(connectionId);
            if(connection == null) {
                LogWarn($"[{caller}] Could not load connection record (connection id: {{0}}, request id: {{}})", CurrentRequest.RequestContext.ConnectionId, CurrentRequest.RequestContext.RequestId);
                return null;
            }

            // fetch the user record associated with this connection
            var user = await _table.GetUserAsync(connection.UserId);
            if(user == null) {
                LogWarn($"[{caller}] Could not load user record (userid: {{0}}, request id: {{}})", connection.UserId, CurrentRequest.RequestContext.RequestId);
                return null;
            }
            return user;
        }
    }
}
