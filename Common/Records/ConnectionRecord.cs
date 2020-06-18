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

    public sealed class ConnectionRecord : ARecord {

        //--- Class Methods ---
        public static async Task<ConnectionRecord> CreateConnectionAsync(Table table, string connectionId, string userId) {
            var result = new ConnectionRecord {
                ConnectionId = connectionId,
                UserId = userId,
            };
            await result.CreateAsync(table);
            return result;
        }

        public static async Task<UserRecord> GetUserByConnectionAsync(Table table, string connectionId)

            // TODO: use reverse look-up to identify which user owns this connection
            => throw new NotImplementedException();

        //--- Properties ---
        public override string PK => USER_PREFIX + UserId;
        public override string SK => CONNECTION_PREFIX + ConnectionId;
        public string UserId { get; set; }
        public string ConnectionId { get; set; }

        //--- Methods ---
        public async Task<IEnumerable<ConnectionRecord>> GetUserConnectionsAsync(Table table) {
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, CONNECTION_PREFIX);
            return await DoSearchAsync<ConnectionRecord>(table.Query(PK, query));
        }
    }
}
