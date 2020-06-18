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

using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DocumentModel;

namespace Demo.WebSocketsChat.Common.Records {

    public sealed class ChannelRecord : ARecord {

        //--- Class Methods ---
        public static Task<ChannelRecord> GetChannelAsync(Table table, string channelId)
            => GetItemAsync<ChannelRecord>(table, CHANNEL_PREFIX + channelId, INFO);

        //--- Properties ---
        public override string PK => CHANNEL_PREFIX + ChannelId;
        public override string SK => INFO;
        public string ChannelId { get; set; }
        public string ChannelName { get; set; }

        //---Methods ----
        public async Task<IEnumerable<UserRecord>> GetChannelUsersAsync(Table table) {
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, USER_PREFIX);
            return await DoSearchAsync<UserRecord>(table.Query(PK, query));
        }
    }
}
