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
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace Demo.WebSocketsChat.Common {

    public abstract class ARecord {

        //--- Properties ---
        public virtual string PK { get; set; }
        public virtual string SK { get; set; }

        //--- Class Methods ---
        public async static Task<IEnumerable<T>> DoSearchAsync<T>(Search search) {
            var results = new List<T>();
            do {
                var documents = await search.GetNextSetAsync();
                results.AddRange(documents.Select(document => JsonConvert.DeserializeObject<T>(document.ToJson())));
            } while(!search.IsDone);
            return results;
        }

        public async static Task<T> GetItemAsync<T>(Table table, string pk, string sk) {
            var record = await table.GetItemAsync(pk, sk);
            return (record != null)
                ? JsonConvert.DeserializeObject<T>(record.ToJson())
                : default;
        }

        public static Task PutItemAsync<T>(Table table, T record)
            => table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(record)));

        public static Task DeleteRowAsync(Table table, string pk, string sk)
            => table.DeleteItemAsync(pk, sk);

        //--- Methods ---
        public Task SaveAsync(Table table) => table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(this)));
        public Task DeleteAsync(Table table) => table.DeleteItemAsync(PK, SK);
    }

    public sealed class UserRecord : ARecord {

        //--- Constants ---
        public const string PK_PREFIX = "USER#";
        public const string SK_VALUE = "INFO";

        //--- Class Methods ---
        public static Task<UserRecord> GetUserRecord(Table table, string id)
            => GetItemAsync<UserRecord>(table, PK_PREFIX + id, SK_VALUE);

        //--- Properties ---
        public override string PK => PK_PREFIX + UserId;
        public override string SK => SK_VALUE;
        public string UserId { get; set; }
        public string UserName { get; set; }

        //--- Methods ---
        public async Task<IEnumerable<ConnectionRecord>> GetConnectionsAsync(Table table) {
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, ConnectionRecord.PK_PREFIX);
            return await DoSearchAsync<ConnectionRecord>(table.Query(PK, query));
        }

        public async Task<IEnumerable<SubscriptionRecord>> GetSubscribedChannelsAsync(Table table) {

            // TODO: specify using the User-to-Channel (Subscription Record Index)
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, ChannelRecord.PREFIX);
            return await DoSearchAsync<SubscriptionRecord>(table.Query(PK, query));
        }

        public Task UpdateAsync(Table table) => PutItemAsync<UserRecord>(table, this);
    }

    public sealed class ConnectionRecord : ARecord {

        //--- Constants ---
        public const string PK_PREFIX = "WS#";

        //--- Properties ---
        public string UserId { get; set; }
        public string ConnectionId { get; set; }

        //--- Methods ---
        public async Task<IEnumerable<ConnectionRecord>> GetUserConnectionsAsync(Table table, string userId){
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, PK_PREFIX);
            return await DoSearchAsync<ConnectionRecord>(table.Query(PK, query));
        }
    }

    public sealed class ChannelRecord : ARecord {

        //--- Constants ---
        public const string PREFIX = "ROOM#";

        //--- Class Methods ---
        public async static Task LeaveChannelAsync(Table table, string userId, string channelId) {
            await new SubscriptionRecord {
                ChannelId = channelId,
                UserId = userId
            }.DeleteAsync(table);
        }

        public static Task<ChannelRecord> GetChannelAsync(Table table, string channelId)
            => ARecord.GetItemAsync(table, PREFIX + channelId, SK);

        //--- Properties ---
        public override string PK => PREFIX + ID;
        public override string SK => "INFO";
        public string ID { get; set; }
        public string ChannelName { get; set; }

        //---Methods ----
        public Task JoinChannelAsync(Table table, string userId)
            => PutItemAsync<SubscriptionRecord>(table, new SubscriptionRecord {
                ChannelId = ID,
                UserId = userId,
                LastSeenTimestamp = 0L
            });

        public async Task<IEnumerable<UserRecord>> GetChannelUsersAsync(Table table, string channelId){
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, UserRecord.PK_PREFIX);
            return await DoSearchAsync<UserRecord>(table.Query(PK, query));
        }

        public Task PutChannelAsync(Table table) => PutItemAsync<ChannelRecord>(table, this);
    }

    public sealed class MessageRecord : ARecord {

        //--- Constants ---
        public const string SK_PREFIX = "WHEN#";

        //--- Constructors ---
        public MessageRecord() {
            SK = "INFO";
        }

        //--- Properties ---
        public override string PK => ChannelRecord.PREFIX + ChannelId;
        public override string SK => SK_PREFIX + Timestamp.ToString("0000000000000000");
        public long Timestamp { get; set; }
        public string UserId { get; set; }
        public string ChannelId { get; set; }
        public string Message { get; set; }

        //--- Methods ---
        public static async Task<MessageRecord> CreateMessage(Table table, string userId, string channelId, string message) {
            var record = new MessageRecord {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = userId,
                ChannelId = channelId,
                Message = message
            };
            await PutItemAsync<MessageRecord>(table, record);
            return record;
        }

        public static async Task<IEnumerable<MessageRecord>> GetMessagesSinceAsync(Table table, string channelId, long timestamp){
            throw new NotImplementedException();
        }
    }

    public sealed class SubscriptionRecord : ARecord {

        //--- Constants ---
        public const string SK_PREFIX = "WHEN#";

        //--- Properties ---
        public override string PK => ChannelRecord.PREFIX + ChannelId;
        public override string SK => UserRecord.PK_PREFIX + UserId;
        public string ChannelId { get; set; }
        public string UserId { get; set; }
        public long LastSeenTimestamp { get; set; }
    }
}
