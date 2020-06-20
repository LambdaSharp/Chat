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

namespace Demo.WebSocketsChat.Common.DynamoDB {

    public sealed class DynamoTable {

        //--- Constants ---
        private const string VALID_SYMBOLS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        //--- Class Methods ---
        public static string GetRandomString(int length)
            => new string(Enumerable.Repeat(VALID_SYMBOLS, length).Select(chars => chars[_random.Next(chars.Length)]).ToArray());

        private async static Task<IEnumerable<T>> DoSearchAsync<T>(Search search, CancellationToken cancellationToken = default) where T : IRecord {
            var results = new List<T>();
            do {
                var documents = await search.GetNextSetAsync(cancellationToken);
                results.AddRange(documents.Select(document => JsonSerializer.Deserialize<T>(document.ToJson())));
            } while(!search.IsDone);
            return results;
        }

        //--- Fields ---
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly Table _table;

        //--- Constructors ---
        public DynamoTable(string tableName, IAmazonDynamoDB dynamoDbClient = null) {
            TableName = tableName ?? throw new System.ArgumentNullException(nameof(tableName));
            _dynamoDbClient = dynamoDbClient ?? new AmazonDynamoDBClient();
            _table = Table.LoadTable(dynamoDbClient, tableName);
        }

        //--- Properties ---
        public string TableName { get; }

        //--- Methods ---
        public async Task<T> GetAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord {
            var record = await _table.GetItemAsync(item.PK, item.SK, cancellationToken);
            return (record != null)
                ? JsonSerializer.Deserialize<T>(record.ToJson())
                : default;
        }

        public async Task<IEnumerable<T>> BathGetAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default) where T : IRecord {
            var batch = _table.CreateBatchGet();
            foreach(var item in items) {
                batch.AddKey(item.PK, item.SK);
            }
            await batch.ExecuteAsync();
            return batch.Results.Where(record => record != null)
                .Select(record => JsonSerializer.Deserialize<T>(record.ToJson()));
        }

        public Task CreateOrUpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord
            => PutItemAsync(item, config: null, cancellationToken);

        public Task CreateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord
            => PutItemAsync(item, new PutItemOperationConfig {
                ConditionalExpression = new Expression {
                    ExpressionStatement = "attribute_not_exists(#PK)",
                    ExpressionAttributeNames = {
                        ["#PK"] = "PK"
                    }
                }
            }, cancellationToken);

        public Task UpdateAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord
            => PutItemAsync(item, new PutItemOperationConfig {
                ConditionalExpression = new Expression {
                    ExpressionStatement = "attribute_exists(#PK)",
                    ExpressionAttributeNames = {
                        ["#PK"] = "PK"
                    }
                }
            }, cancellationToken);

        public Task DeleteAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord {
            var tasks = new List<Task>();

            // delete record
            tasks.Add(_table.DeleteItemAsync(item.PK, item.SK));

            // delete projections
            if(item is IRecordProjected<T> projectedItem) {
                foreach(var projection in projectedItem.Projections) {
                    tasks.Add(_table.DeleteItemAsync(projection.GetPK(item), projection.GetSK(item), cancellationToken));
                }
            }
            return Task.WhenAll(tasks);
        }

        public Task<IEnumerable<S>> GetAllSecondaryRecordsAsync<P, S>(P item, CancellationToken cancellationToken = default)
            where P : IRecord
            where S : IRecord, ISecondaryRecord<P>, new()
            => DoSearchAsync<S>(_table.Query(item.PK, new QueryFilter("SK", QueryOperator.BeginsWith, new S().SKPrefix)), cancellationToken);

        private Task PutItemAsync<T>(T item, PutItemOperationConfig config, CancellationToken cancellationToken = default) where T : IRecord {
            var json = JsonSerializer.Serialize(item);
            var tasks = new List<Task> {

                // store record
                PutAsync(item.PK, item.SK)
            };

            // store all projections of the record
            if(item is IRecordProjected<T> projectedItem) {
                tasks.AddRange(projectedItem.Projections.Select(projection => PutAsync(projection.GetPK(item), projection.GetSK(item))));
            }
            return Task.WhenAll(tasks);

            // local functions
            Task PutAsync(string pk, string sk) {
                var document = Document.FromJson(json);
                document["PK"] = pk;
                document["SK"] = sk;
                return _table.PutItemAsync(document, config, cancellationToken);
            }
        }
    }
}
