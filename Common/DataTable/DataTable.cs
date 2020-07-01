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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Demo.WebSocketsChat.Common.Records;

namespace Demo.WebSocketsChat.Common.DataStore {

    public sealed class DataTable {

        //--- Constants ---
        private const string VALID_SYMBOLS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string USER_PREFIX = "USER#";
        private const string CHANNEL_PREFIX = "ROOM#";
        private const string CONNECTION_PREFIX = "WS#";
        private const string TIMESTAMP_PREFIX = "WHEN#";
        private const string INFO = "INFO";

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        //--- Class Methods ---
        public static string GetRandomString(int length)
            => new string(Enumerable.Repeat(VALID_SYMBOLS, length).Select(chars => chars[_random.Next(chars.Length)]).ToArray());

        private static T Deserialize<T>(Document record)
            => (record != null)
                ? JsonSerializer.Deserialize<T>(record.ToJson())
                : default;

        private async static Task<IEnumerable<T>> DoSearchAsync<T>(Search search, CancellationToken cancellationToken = default) {
            var results = new List<T>();
            do {
                var documents = await search.GetNextSetAsync(cancellationToken);
                results.AddRange(documents.Select(document => Deserialize<T>(document)));
            } while(!search.IsDone);
            return results;
        }

        //--- Fields ---
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly Table _table;

        //--- Constructors ---
        public DataTable(string tableName, IAmazonDynamoDB dynamoDbClient = null) {
            TableName = tableName ?? throw new System.ArgumentNullException(nameof(tableName));
            _dynamoDbClient = dynamoDbClient ?? new AmazonDynamoDBClient();
            _table = Table.LoadTable(dynamoDbClient, tableName);
        }

        //--- Properties ---
        public string TableName { get; }

        private PutItemOperationConfig CreateItemConfig => new PutItemOperationConfig {
            ConditionalExpression = new Expression {
                ExpressionStatement = "attribute_not_exists(#PK)",
                ExpressionAttributeNames = {
                    ["#PK"] = "PK"
                }
            }
        };

        private PutItemOperationConfig UpdateItemConfig => new PutItemOperationConfig {
            ConditionalExpression = new Expression {
                ExpressionStatement = "attribute_exists(#PK)",
                ExpressionAttributeNames = {
                    ["#PK"] = "PK"
                }
            }
        };

        //--- Methods ---

        #region User Record
        public async Task<UserRecord> GetUserAsync(string userId, CancellationToken cancellationToken = default)
            => Deserialize<UserRecord>(await _table.GetItemAsync(USER_PREFIX + userId, INFO, cancellationToken));

        public Task CreateUserAsync(UserRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: USER_PREFIX + record.UserId, SK: INFO)
            }, CreateItemConfig, cancellationToken);

        public Task UpdateUserAsync(UserRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: USER_PREFIX + record.UserId, SK: INFO)
            }, UpdateItemConfig, cancellationToken);

        public Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: USER_PREFIX + userId, SK: INFO )
            }, cancellationToken);
        #endregion

        #region Connection Record
        public async Task<ConnectionRecord> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Deserialize<ConnectionRecord>(await _table.GetItemAsync(CONNECTION_PREFIX + connectionId, INFO, cancellationToken));

        public Task CreateConnectionAsync(ConnectionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: CONNECTION_PREFIX + record.ConnectionId, SK: INFO),
                (PK: USER_PREFIX + record.UserId, SK: CONNECTION_PREFIX + record.ConnectionId)
            }, CreateItemConfig, cancellationToken);

        public Task DeleteConnectionAsync(string connectionId, string userId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: CONNECTION_PREFIX + connectionId, SK: INFO ),
                (PK: USER_PREFIX + userId, SK: CONNECTION_PREFIX + connectionId)
            }, cancellationToken);
        #endregion

        #region Channel Record
        public async Task<UserRecord> GetChannelAsync(string channelId, CancellationToken cancellationToken = default)
            => Deserialize<UserRecord>(await _table.GetItemAsync(CHANNEL_PREFIX + channelId, INFO, cancellationToken));

        public Task CreateChannelAsync(ChannelRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: CHANNEL_PREFIX + record.ChannelId, SK: INFO)
            }, CreateItemConfig, cancellationToken);

        public Task UpdateChannelAsync(ChannelRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: CHANNEL_PREFIX + record.ChannelId, SK: INFO)
            }, UpdateItemConfig, cancellationToken);

        public Task DeleteChannelAsync(string channelId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: CHANNEL_PREFIX + channelId, SK: INFO )
            }, cancellationToken);
        #endregion

        #region Subscription Record
        public Task CreateSubscriptionAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: CHANNEL_PREFIX + record.ChannelId, SK: USER_PREFIX + record.UserId),
                (PK: USER_PREFIX + record.UserId, SK: CHANNEL_PREFIX + record.ChannelId)
            }, CreateItemConfig, cancellationToken);

        public Task UpdateSubscriptionAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(record, new[] {
                (PK: CHANNEL_PREFIX + record.ChannelId, SK: USER_PREFIX + record.UserId),
                (PK: USER_PREFIX + record.UserId, SK: CHANNEL_PREFIX + record.ChannelId)
            }, UpdateItemConfig, cancellationToken);

        public Task DeleteSubscriptionAsync(string channelId, string userId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: CHANNEL_PREFIX + channelId, SK: USER_PREFIX + userId),
                (PK: USER_PREFIX + userId, SK: CHANNEL_PREFIX + channelId)
            }, cancellationToken);
        #endregion

        #region Message Record
        public Task CreateMessageAsync(MessageRecord record, CancellationToken cancellationToken = default) {

            // two messages could be created at the same time; use jitter to disambiguate
            record.Jitter = GetRandomString(4);

            // store record
            return PutItemsAsync(record, new[] {
                (PK: CHANNEL_PREFIX + record.ChannelId, SK: TIMESTAMP_PREFIX + record.Timestamp.ToString("0000000000000000") + "|" + record.Jitter)
            }, CreateItemConfig, cancellationToken);
        }
        #endregion

        #region Cross Record Queries
        public Task<IEnumerable<ConnectionRecord>> GetUserConnectionsAsync(string userId, CancellationToken cancellationToken = default)
            => DoSearchAsync<ConnectionRecord>(_table.Query(USER_PREFIX + userId, new QueryFilter("SK", QueryOperator.BeginsWith, CONNECTION_PREFIX)), cancellationToken);

        public Task<IEnumerable<SubscriptionRecord>> GetUserSubscriptionsAsync(string userId, CancellationToken cancellationToken = default)
            => DoSearchAsync<SubscriptionRecord>(_table.Query(USER_PREFIX + userId, new QueryFilter("SK", QueryOperator.BeginsWith, CHANNEL_PREFIX)), cancellationToken);

        public Task<IEnumerable<MessageRecord>> GetChannelMessagesAsync(string channelId, long sinceTimestamp, CancellationToken cancellationToken = default)
            => DoSearchAsync<MessageRecord>(_table.Query(CHANNEL_PREFIX + channelId, new QueryFilter("SK", QueryOperator.GreaterThanOrEqual, TIMESTAMP_PREFIX + sinceTimestamp.ToString("0000000000000000"))), cancellationToken);
        #endregion

        private Task PutItemsAsync<T>(T item, IEnumerable<(string PK, string SK)> keys, PutItemOperationConfig config, CancellationToken cancellationToken = default) {
            var json = JsonSerializer.Serialize(item);
            return Task.WhenAll(keys.Select(key => _table.PutItemAsync(CreateDocument(key.PK, key.SK), config, cancellationToken)));

            // local functions
            Document CreateDocument(string pk, string sk) {
                var document = Document.FromJson(json);
                document["PK"] = pk;
                document["SK"] = sk;
                document["_Type"] = item.GetType().Name;
                return document;
            }
        }

        private Task DeleteItemsAsync(IEnumerable<(string PK, string SK)> keys, CancellationToken cancellationToken = default)
            => Task.WhenAll(keys.Select(key => _table.DeleteItemAsync(key.PK, key.SK)));
    }
}
