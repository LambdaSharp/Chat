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
using Demo.WebSocketsChat.Common.Notifications;
using Demo.WebSocketsChat.Common.Requests;
using System.Collections.Generic;

namespace Demo.WebSocketsChat.ChatFunction {

    public sealed class Function : ALambdaApiGatewayFunction {

        //--- Fields ---
        private DataTable _dataTable;
        private string _notifyQueueUrl;
        private IAmazonSQS _sqsClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var dataTableName = config.ReadDynamoDBTableName("DataTable");
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");

            // initialize AWS clients
            _sqsClient = new AmazonSQSClient();
            _dataTable = new DataTable(dataTableName, new AmazonDynamoDBClient());
        }

        public async Task OpenConnectionAsync(APIGatewayProxyRequest request, string userId = null) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");

            // check if a user already exists or create a new one
            UserRecord user = null;
            if(userId != null) {
                user = await _dataTable.GetUserAsync(userId);

                // could not find user, reset user id
                if(user == null) {
                    userId = null;
                }
            }
            if(userId == null) {
                userId = DataTable.GetRandomString(6);
            }

            // check if a new user is joining
            if(user == null) {

                // create user record
                user = new UserRecord {
                    UserId = userId,
                    UserName = $"Anonymous-{userId}"
                };
                await _dataTable.CreateUserAsync(user);

                // create user subscription to "General" channel
                await _dataTable.CreateSubscriptionAsync(new SubscriptionRecord {
                    ChannelId = "General",
                    UserId = user.UserId
                });
                await NotifyAsync(userId: null, channelId: null, new JoinedChannelNotification {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    ChannelId = "General"
                }, delay: 1 /* TODO: only here b/c of the $connect race condition */);
            }

            // create connection record
            var connection = new ConnectionRecord {
                ConnectionId = request.RequestContext.ConnectionId,
                UserId = user.UserId
            };
            await _dataTable.CreateConnectionAsync(connection);

            // notify all connections about renamed user
            await NotifyAsync(userId: userId, channelId: null, new WelcomeNotification {
                UserId = user.UserId,
                UserName = user.UserName
            }, delay: 1 /* TODO: only here b/c of the $connect race condition */);

            // fetch all channels the user is subscribed to
            var subscriptions = await _dataTable.GetUserSubscriptionsAsync(user.UserId);
            var users = new Dictionary<string, UserRecord>();
            foreach(var subscription in subscriptions) {

                // fetch all messages the user may have missed since last time they were connected
                var messages = await _dataTable.GetChannelMessagesAsync(subscription.ChannelId, subscription.LastSeenTimestamp);
                foreach(var message in messages) {
                    if(!users.TryGetValue(message.UserId, out var messageUser)) {
                        messageUser = await _dataTable.GetUserAsync(message.UserId);
                        users.Add(message.UserId, messageUser);
                    }

                    // notify user about missed messages
                    await NotifyAsync(user.UserId, channelId: null, new UserMessageChangedNotification {
                        UserId = message.UserId,
                        UserName = messageUser.UserName,
                        ChannelId = message.ChannelId,
                        Text = message.Message,
                        Timestamp = message.Timestamp
                    }, delay: 1 /* TODO: only here b/c of the $connect race condition */);
                }
            }
        }

        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);

            // delete closed connection record
            await _dataTable.DeleteConnectionAsync(CurrentRequest.RequestContext.ConnectionId, user.UserId);
        }

        public async Task SendMessageAsync(SendMessageRequest request) {

            // NOTE: this method is invoked by messages with the "send" route key

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                return;
            }

            // store message
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _dataTable.CreateMessageAsync(new MessageRecord {
                Timestamp = now,
                ChannelId = request.ChannelId,
                Message = request.Text,
                UserId = user.UserId
            });

            // notify all connections about new message
            await NotifyAsync(userId: null, channelId: request.ChannelId, new UserMessageChangedNotification {
                UserId = user.UserId,
                UserName = user.UserName,
                ChannelId = request.ChannelId,
                Text = request.Text,
                Timestamp = now
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
            await _dataTable.UpdateUserAsync(user);

            // notify all connections about renamed user
            await NotifyAsync(userId: null, channelId: null, new UserNameChangedNotification {
                UserId = user.UserId,
                UserName = user.UserName,
                OldUserName = oldUserName
            });
        }

        private Task NotifyAsync<T>(string userId, string channelId, T notification, int delay = 0) where T : Notification
            => _sqsClient.SendMessageAsync(new Amazon.SQS.Model.SendMessageRequest {
                    MessageBody = LambdaSerializer.Serialize(new BroadcastMessage {
                        UserId = userId,
                        ChannelId = channelId,
                        Payload = LambdaSerializer.Serialize(notification)
                    }
                ),
                QueueUrl = _notifyQueueUrl,
                DelaySeconds = delay
            });

        private async Task<UserRecord> GetUserFromConnectionId(string connectionId, [CallerMemberName] string caller = "") {

            // fetch the connection record associated with this connection ID
            var connection = await _dataTable.GetConnectionAsync(connectionId);
            if(connection == null) {
                LogWarn($"[{caller}] Could not load connection record (connection id: {{0}}, request id: {{}})", CurrentRequest.RequestContext.ConnectionId, CurrentRequest.RequestContext.RequestId);
                return null;
            }

            // fetch the user record associated with this connection
            var user = await _dataTable.GetUserAsync(connection.UserId);
            if(user == null) {
                LogWarn($"[{caller}] Could not load user record (userid: {{0}}, request id: {{}})", connection.UserId, CurrentRequest.RequestContext.RequestId);
                return null;
            }
            return user;
        }
    }
}
