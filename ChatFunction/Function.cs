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
using LambdaSharp.Chat.Common;
using LambdaSharp.Chat.Common.Records;
using System.Runtime.CompilerServices;
using LambdaSharp.Chat.Common.DataStore;
using LambdaSharp.Chat.Common.Notifications;
using LambdaSharp.Chat.Common.Requests;
using System.Collections.Generic;
using System.Linq;

namespace LambdaSharp.Chat.ChatFunction {

    public sealed class Function : ALambdaApiGatewayFunction {

        //--- Fields ---
        private DataTable _dataTable;
        private string _notifyQueueUrl;
        private IAmazonSQS _sqsClient;

        //--- Constructors ---
        public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            var dataTableName = config.ReadDynamoDBTableName("DataTable");
            _notifyQueueUrl = config.ReadSqsQueueUrl("NotifyQueue");

            // initialize AWS clients
            _sqsClient = new AmazonSQSClient();
            _dataTable = new DataTable(dataTableName, new AmazonDynamoDBClient());
        }

        // [Route("$connect")]
        public async Task OpenConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Connected: {request.RequestContext.ConnectionId}");

            // get the user id from the authorizer metadata
            request.RequestContext.Authorizer.TryGetValue("sub", out var subject);
            var userId = subject as string;
            if(userId == null) {

                // TODO: improve exception
                throw new Exception("Unauthenticated access detected");
            }

            // create connection record
            var connection = new ConnectionRecord {
                ConnectionId = request.RequestContext.ConnectionId,
                UserId = userId
            };
            await _dataTable.CreateConnectionAsync(connection);

            // check if a user already exists or create a new one
            var user = await _dataTable.GetUserAsync(userId);
            if(user == null) {

                // check if a user name was requested; otherwise, generate one
                string userName;
                if (
                    request.RequestContext.Authorizer.TryGetValue("cognito:username", out var userNameObject)
                    && (userNameObject is string userNameText)
                ) {
                    userName = userNameText;
                } else {
                    userName = $"User-{DataTable.GetRandomString(6)}";
                }

                // create user record
                user = new UserRecord {
                    UserId = userId,
                    UserName = userName
                };
                await _dataTable.CreateUserAsync(user);

                // create user subscription to "General" channel
                await _dataTable.CreateSubscriptionAsync(new SubscriptionRecord {
                    ChannelId = "General",
                    UserId = user.UserId
                });

                // TODO: consider making this a channel message -or- react to data-table changes
                await NotifyAsync(userId: null, channelId: null, new JoinedChannelNotification {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    ChannelId = "General"
                }, delay: 1 /* TODO: only here b/c of the $connect race condition */);
            }
        }

        // [Route("$disconnect")]
        public async Task CloseConnectionAsync(APIGatewayProxyRequest request) {
            LogInfo($"Disconnected: {request.RequestContext.ConnectionId}");

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);

            // delete closed connection record
            await _dataTable.DeleteConnectionAsync(CurrentRequest.RequestContext.ConnectionId, user.UserId);
        }

        // [Route("hello")]
        public async Task<WelcomeNotification> HelloAsync(HelloRequest request) {

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                throw new Exception("Unknown user");
            }

            // fetch all channels the user is subscribed to
            var subscriptions = await _dataTable.GetUserSubscriptionsAsync(user.UserId);
            var welcomeUsers = new Dictionary<string, UserRecord>();
            var welcomeChannels = new List<ChannelRecord>();
            var welcomeChannelMessages = new Dictionary<string, IEnumerable<MessageRecord>>();
            foreach(var subscription in subscriptions) {

                // TODO: lookup channels in batch
                welcomeChannels.Add(await _dataTable.GetChannelAsync(subscription.ChannelId));

                // fetch all messages the user may have missed since last time they were connected
                var messages = await _dataTable.GetChannelMessagesAsync(subscription.ChannelId, subscription.LastSeenTimestamp);
                var channelMessages = new List<MessageRecord>();
                foreach(var message in messages) {
                    channelMessages.Add(message);

                    // TODO: lookup users in batch
                    if(!welcomeUsers.TryGetValue(message.UserId, out var messageUser)) {
                        messageUser = await _dataTable.GetUserAsync(message.UserId);
                        welcomeUsers.Add(message.UserId, messageUser);
                    }
                }
                welcomeChannelMessages.Add(subscription.ChannelId, channelMessages);
            }

            // inform new connection about user state
            return new WelcomeNotification {
                UserId = user.UserId,
                UserName = user.UserName,
                Channels = welcomeChannels.OrderBy(channel => channel.ChannelName.ToLowerInvariant()).ToList(),
                Users = welcomeUsers.Values.ToList(),
                ChannelMessages = welcomeChannelMessages
            };
        }

        // [Route("send")]
        public async Task SendMessageAsync(SendMessageRequest request) {

            // fetch the user record associated with this connection id
            var user = await GetUserFromConnectionId(CurrentRequest.RequestContext.ConnectionId);
            if(user == null) {
                return;
            }

            // TODO: validate that the user is a member of this channel

            // store message
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _dataTable.CreateMessageAsync(new MessageRecord {
                Timestamp = now,
                ChannelId = request.ChannelId,
                Message = request.Text,
                UserId = user.UserId
            });

            // notify all connections about new message
            await NotifyAsync(userId: null, channelId: request.ChannelId, new UserMessageNotification {
                UserId = user.UserId,
                UserName = user.UserName,
                ChannelId = request.ChannelId,
                Text = request.Text,
                Timestamp = now
            });
        }

        // [Route("rename")]
        public async Task RenameUserAsync(RenameUserRequest request) {

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

        private Task NotifyAsync<T>(string userId, string channelId, T notification, int delay = 0) where T : ANotification
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
