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

        //--- Types ---
        private class ReverseLookup : ARecord {

            //--- Constructors ---
            public ReverseLookup(ConnectionRecord record) {
                ConnectionId = record.ConnectionId;
                UserId = record.UserId;
            }

            //--- Properties ---
            public override string PK => USER_PREFIX + UserId;
            public override string SK => CONNECTION_PREFIX + ConnectionId;
            public string ConnectionId { get; }
            public string UserId { get; }
        }

        //--- Class Methods ---
        public static async Task<ConnectionRecord> CreateConnectionAsync(Table table, string connectionId, string userId) {

            // create the connetion record
            var result = new ConnectionRecord {
                ConnectionId = connectionId,
                UserId = userId
            };
            await result.CreateAsync(table);

            // create the reverse-lookup record
            await new ReverseLookup(result).CreateAsync(table);
            return result;
        }

        public static Task<ConnectionRecord> GetConnectionAsync(Table table, string connectionId)
            => GetItemAsync<ConnectionRecord>(table, CONNECTION_PREFIX + connectionId, INFO);

        public async Task<IEnumerable<ConnectionRecord>> GetConnectionsByUserAsync(Table table, string userId) {

            // use reverse-lookup to find connections opened by user
            var query = new QueryFilter("SK", QueryOperator.BeginsWith, CONNECTION_PREFIX);
            return await DoSearchAsync<ConnectionRecord>(table.Query(USER_PREFIX + userId, query));
        }


        //--- Properties ---
        public override string PK => CONNECTION_PREFIX + ConnectionId;
        public override string SK => INFO;
        public string ConnectionId { get; set; }
        public string UserId { get; set; }

        //--- Methods ---
        public override async Task DeleteAsync(Table table) {
            await base.DeleteAsync(table);

            // also delete the reverse-lookup record
            await new ReverseLookup(this).DeleteAsync(table);
        }
    }
}
