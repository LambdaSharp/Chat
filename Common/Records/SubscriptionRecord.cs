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
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DocumentModel;

namespace Demo.WebSocketsChat.Common.Records {

    public sealed class SubscriptionRecord : ARecord {

        //--- Types ---
        private class ReverseLookup : ARecord {

            //--- Constructors ---
            public ReverseLookup(SubscriptionRecord record) {
                    ChannelId = record.ChannelId;
                    UserId = record.UserId;
                    LastSeenTimestamp = record.LastSeenTimestamp;
            }

            //--- Properties ---
            public override string PK => USER_PREFIX + UserId;
            public override string SK => CHANNEL_PREFIX + ChannelId;
            public string ChannelId { get; }
            public string UserId { get; }
            public long LastSeenTimestamp { get; }
        }

        //--- Class Methods ---
        public static async Task<SubscriptionRecord> JoinChannelAsync(Table table, string channelId, string userId) {

            // create subscription record
            var result = new SubscriptionRecord {
                ChannelId = channelId,
                UserId = userId,
                LastSeenTimestamp = 0L
            };
            await result.CreateAsync(table);

            // create the reverse-lookup record
            await new ReverseLookup(result).CreateAsync(table);
            return result;
        }

        public static async Task LeaveChannelAsync(Table table, string channelId, string userId)
            => await new SubscriptionRecord {
                ChannelId = channelId,
                UserId = userId
            }.DeleteAsync(table);

        public static async Task<IEnumerable<SubscriptionRecord>> GetSubscriptionsByUserAsync(Table table, string userId) {

            // use reverse-lookup to find subscriptions belonging to user
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, CHANNEL_PREFIX);
            return await DoSearchAsync<SubscriptionRecord>(table.Query(USER_PREFIX + userId, query));
        }

        //--- Properties ---
        public override string PK => CHANNEL_PREFIX + ChannelId;
        public override string SK => USER_PREFIX + UserId;
        public string ChannelId { get; set; }
        public string UserId { get; set; }
        public long LastSeenTimestamp { get; set; }

        //--- Methods ---
        public async Task<IEnumerable<MessageRecord>> GetNewMessagesAsync(Table table) {

            // TODO: implement query that fetches all messages in the channel since the last seen timestamp
            throw new NotImplementedException();
        }

        //--- Methods ---
        public override async Task DeleteAsync(Table table) {
            await base.DeleteAsync(table);

            // also delete the reverse-lookup record
            await new ReverseLookup(this).DeleteAsync(table);
        }
    }
}
