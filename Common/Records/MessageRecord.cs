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
using Amazon.DynamoDBv2.DocumentModel;

namespace Demo.WebSocketsChat.Common.Records {

    public sealed class MessageRecord : ARecord {

        //--- Class Methods ---
        public static async Task<MessageRecord> CreateMessage(Table table, string userId, string channelId, string message) {
            var record = new MessageRecord {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = userId,
                ChannelId = channelId,
                Message = message,
                Jitter = RandomString(8)
            };
            await record.CreateAsync(table);
            return record;
        }

        //--- Properties ---
        public override string PK => CHANNEL_PREFIX + ChannelId;
        public override string SK => TIMESTAMP_PREFIX + Timestamp.ToString("0000000000000000") + "|" + Jitter;
        public long Timestamp { get; set; }
        public string UserId { get; set; }
        public string ChannelId { get; set; }
        public string Message { get; set; }
        public string Jitter { get; set; }
    }
}
