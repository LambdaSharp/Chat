/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2019
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

    public class ConnectionUser {

        //--- Properties ---
        public string ConnectionId { get; set; }
        public string UserName { get; set; }
    }

    public class ConnectionsTable {

        //--- Fields ---
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly Table _table;

        //--- Constructors ---
        public ConnectionsTable(
            string tableName,
            IAmazonDynamoDB dynamoDbClient
        ) {
            _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
            _table = Table.LoadTable(
                _dynamoDbClient,
                tableName ?? throw new ArgumentNullException(nameof(tableName))
            );
        }

        //--- Methods ---
        public Task PutRowAsync<T>(T record) => _table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(record)));

        public async Task<T> GetRowAsync<T>(string id) {
            var record = await _table.GetItemAsync(id);
            return (record != null)
                ? JsonConvert.DeserializeObject<T>(record.ToJson())
                : default;
        }

        public async Task<IEnumerable<string>> GetAllRowsAsync() {
            return (await _dynamoDbClient.ScanAsync(new ScanRequest {
                TableName = _table.TableName,
                ProjectionExpression = "ConnectionId"
            }))
                .Items
                .Select(item => item["ConnectionId"].S)
                .ToList();
        }

        public Task DeleteRowAsync(string id) => _table.DeleteItemAsync(id);
    }
}
