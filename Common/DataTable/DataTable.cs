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

    public static class TableEx {

        //--- Extension Methods ---
        public static Search QueryBeginsWith(this Table table, Primitive hashKey, Primitive rangeKeyPrefix) {
            var filter = new QueryFilter();
            filter.AddCondition("PK", QueryOperator.Equal, new DynamoDBEntry[] { hashKey });
            filter.AddCondition("SK", QueryOperator.BeginsWith, new DynamoDBEntry[] { rangeKeyPrefix });
            return table.Query(new QueryOperationConfig {
                Filter = filter
            });
        }

        public static Search QueryGS1BeginsWith(this Table table, Primitive hashKey, Primitive rangeKeyPrefix) {
            var filter = new QueryFilter();
            filter.AddCondition("GS1PK", QueryOperator.Equal, new DynamoDBEntry[] { hashKey });
            filter.AddCondition("GS1SK", QueryOperator.BeginsWith, new DynamoDBEntry[] { rangeKeyPrefix });
            return table.Query(new QueryOperationConfig {
                IndexName = "GS1",
                Filter = filter
            });
        }
    }

    public sealed class DataTable {

        //--- Constants ---
        private const string VALID_SYMBOLS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string USER_PREFIX = "USER#";
        private const string CHANNEL_PREFIX = "ROOM#";
        private const string CONNECTION_PREFIX = "WS#";
        private const string TIMESTAMP_PREFIX = "WHEN#";
        private const string INFO = "INFO";
        private const string USERS = "USERS";
        private const string CHANNELS = "ROOMS";

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        private readonly static PutItemOperationConfig CreateItemConfig = new PutItemOperationConfig {
            ConditionalExpression = new Expression {
                ExpressionStatement = "attribute_not_exists(#PK)",
                ExpressionAttributeNames = {
                    ["#PK"] = "PK"
                }
            }
        };

        private readonly static PutItemOperationConfig UpdateItemConfig = new PutItemOperationConfig {
            ConditionalExpression = new Expression {
                ExpressionStatement = "attribute_exists(#PK)",
                ExpressionAttributeNames = {
                    ["#PK"] = "PK"
                }
            }
        };

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

        //--- Methods ---

        #region User Record
        public async Task<UserRecord> GetUserAsync(string userId, CancellationToken cancellationToken = default)
            => Deserialize<UserRecord>(await _table.GetItemAsync(USER_PREFIX + userId, INFO, cancellationToken));

        public Task CreateUserAsync(UserRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: USER_PREFIX + record.UserId,
                sk: INFO,
                gs1pk: USERS,
                gs1sk: USER_PREFIX + record.UserId,
                CreateItemConfig,
                cancellationToken
            );

        public Task UpdateUserAsync(UserRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: USER_PREFIX + record.UserId,
                sk: INFO,
                gs1pk: USERS,
                gs1sk: USER_PREFIX + record.UserId,
                UpdateItemConfig,
                cancellationToken
            );

        public Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: USER_PREFIX + userId, SK: INFO)
            }, cancellationToken);
        #endregion

        #region Connection Record
        public async Task<ConnectionRecord> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
            => Deserialize<ConnectionRecord>(await _table.GetItemAsync(CONNECTION_PREFIX + connectionId, INFO, cancellationToken));

        public Task CreateConnectionAsync(ConnectionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: CONNECTION_PREFIX + record.ConnectionId,
                sk: INFO,
                gs1pk: USER_PREFIX + record.UserId,
                gs1sk: CONNECTION_PREFIX + record.ConnectionId,
                CreateItemConfig,
                cancellationToken
            );

        public Task DeleteConnectionAsync(string connectionId, string userId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: CONNECTION_PREFIX + connectionId, SK: INFO )
            }, cancellationToken);
        #endregion

        #region Channel Record
        public async Task<ChannelRecord> GetChannelAsync(string channelId, CancellationToken cancellationToken = default)
            => Deserialize<ChannelRecord>(await _table.GetItemAsync(CHANNEL_PREFIX + channelId, INFO, cancellationToken));

        public Task CreateChannelAsync(ChannelRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: CHANNEL_PREFIX + record.ChannelId,
                sk: INFO,
                gs1pk: CHANNELS,
                gs1sk: CHANNEL_PREFIX + record.ChannelId,
                CreateItemConfig,
                cancellationToken
            );

        public Task UpdateChannelAsync(ChannelRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: CHANNEL_PREFIX + record.ChannelId,
                sk: INFO,
                gs1pk: CHANNELS,
                gs1sk: CHANNEL_PREFIX + record.ChannelId,
                UpdateItemConfig,
                cancellationToken
            );

        public Task DeleteChannelAsync(string channelId, CancellationToken cancellationToken = default)
            => DeleteItemsAsync(new[] {
                (PK: CHANNEL_PREFIX + channelId, SK: INFO )
            }, cancellationToken);
        #endregion

        #region Subscription Record
        public async Task<SubscriptionRecord> GetSubscriptionAsync(string userId, string channelId, CancellationToken cancellationToken = default)
            => Deserialize<SubscriptionRecord>(await _table.GetItemAsync(CHANNEL_PREFIX + channelId, USER_PREFIX + userId, cancellationToken));

        public Task CreateSubscriptionAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: CHANNEL_PREFIX + record.ChannelId,
                sk: USER_PREFIX + record.UserId,
                gs1pk: USER_PREFIX + record.UserId,
                gs1sk: CHANNEL_PREFIX + record.ChannelId,
                CreateItemConfig,
                cancellationToken
            );

        public Task UpdateSubscriptionAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
            => PutItemsAsync(
                record,
                pk: CHANNEL_PREFIX + record.ChannelId,
                sk: USER_PREFIX + record.UserId,
                gs1pk: USER_PREFIX + record.UserId,
                gs1sk: CHANNEL_PREFIX + record.ChannelId,
                UpdateItemConfig,
                cancellationToken
            );

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
            return PutItemsAsync(
                record,
                pk: CHANNEL_PREFIX + record.ChannelId,
                sk: TIMESTAMP_PREFIX + record.Timestamp.ToString("0000000000000000") + "#" + record.Jitter,
                CreateItemConfig,
                cancellationToken
            );
        }
        #endregion

        #region Record Queries
        public Task<IEnumerable<UserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default)
            => DoSearchAsync<UserRecord>(_table.QueryGS1BeginsWith(USERS, USER_PREFIX), cancellationToken);

        public Task<IEnumerable<ChannelRecord>> GetAllChannelsAsync(CancellationToken cancellationToken = default)
            => DoSearchAsync<ChannelRecord>(_table.QueryGS1BeginsWith(CHANNELS, CHANNEL_PREFIX), cancellationToken);

        public Task<IEnumerable<ConnectionRecord>> GetUserConnectionsAsync(string userId, CancellationToken cancellationToken = default)
            => DoSearchAsync<ConnectionRecord>(_table.QueryGS1BeginsWith(USER_PREFIX + userId, CONNECTION_PREFIX), cancellationToken);

        public Task<IEnumerable<SubscriptionRecord>> GetChannelSubscriptionsAsync(string channelId, CancellationToken cancellationToken = default)
            => DoSearchAsync<SubscriptionRecord>(_table.QueryBeginsWith(CHANNEL_PREFIX + channelId, USER_PREFIX), cancellationToken);

        public Task<IEnumerable<SubscriptionRecord>> GetUserSubscriptionsAsync(string userId, CancellationToken cancellationToken = default)
            => DoSearchAsync<SubscriptionRecord>(_table.QueryGS1BeginsWith(USER_PREFIX + userId, CHANNEL_PREFIX), cancellationToken);

        public Task<IEnumerable<MessageRecord>> GetChannelMessagesAsync(string channelId, long sinceTimestamp, CancellationToken cancellationToken = default)
            => DoSearchAsync<MessageRecord>(_table.Query(CHANNEL_PREFIX + channelId, new QueryFilter("SK", QueryOperator.GreaterThanOrEqual, TIMESTAMP_PREFIX + sinceTimestamp.ToString("0000000000000000"))), cancellationToken);
        #endregion

        private Task PutItemsAsync<T>(T item, string pk, string sk, PutItemOperationConfig config, CancellationToken cancellationToken = default) {
            var document = Document.FromJson(JsonSerializer.Serialize(item));
            document["_Type"] = item.GetType().Name;
            document["PK"] = pk ?? throw new ArgumentNullException(nameof(pk));
            document["SK"] = sk ?? throw new ArgumentNullException(nameof(sk));
            return _table.PutItemAsync(document, config, cancellationToken);
        }

        private Task PutItemsAsync<T>(T item, string pk, string sk, string gs1pk, string gs1sk, PutItemOperationConfig config, CancellationToken cancellationToken = default) {
            var document = Document.FromJson(JsonSerializer.Serialize(item));
            document["_Type"] = item.GetType().Name;
            document["PK"] = pk ?? throw new ArgumentNullException(nameof(pk));
            document["SK"] = sk ?? throw new ArgumentNullException(nameof(sk));
            document["GS1PK"] = gs1pk ?? throw new ArgumentNullException(nameof(gs1pk));
            document["GS1SK"] = gs1sk ?? throw new ArgumentNullException(nameof(gs1sk));
            return _table.PutItemAsync(document, config, cancellationToken);
        }

        private Task DeleteItemsAsync(IEnumerable<(string PK, string SK)> keys, CancellationToken cancellationToken = default)
            => Task.WhenAll(keys.Select(key => _table.DeleteItemAsync(key.PK, key.SK)));
    }
}
