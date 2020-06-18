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

        public static Task JoinChannelAsync(Table table, string channelId, string userId)
            => new SubscriptionRecord {
                ChannelId = channelId,
                UserId = userId,
                LastSeenTimestamp = 0L
            }.CreateAsync(table);

        public static async Task LeaveChannelAsync(Table table, string channelId, string userId)
            => await new SubscriptionRecord {
                ChannelId = channelId,
                UserId = userId
            }.DeleteAsync(table);

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
    }
}
