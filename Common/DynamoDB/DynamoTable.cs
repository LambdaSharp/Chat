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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

namespace Demo.WebSocketsChat.Common.DynamoDB {

    public sealed class DynamoTable {

        //--- Class Methods ---
        private async static Task<IEnumerable<T>> DoSearchAsync<T>(Search search, CancellationToken cancellationToken = default) where T : IRecord {
            var results = new List<T>();
            do {
                var documents = await search.GetNextSetAsync(cancellationToken);
                results.AddRange(documents.Select(document => JsonSerializer.Deserialize<T>(document.ToJson())));
            } while(!search.IsDone);
            return results;
        }

        //--- Constructors ---
        public DynamoTable(string tableName, IAmazonDynamoDB dynamoDbClient) {
            TableName = tableName ?? throw new System.ArgumentNullException(nameof(tableName));
            DynamoDbClient = dynamoDbClient ?? throw new System.ArgumentNullException(nameof(dynamoDbClient));
            Table = Table.LoadTable(dynamoDbClient, tableName);
        }

        //--- Properties ---
        public string TableName { get; }
        private IAmazonDynamoDB DynamoDbClient { get; }
        private Table Table { get; }

        //--- Methods ---
        public async Task<T> GetAsync<T>(T item, CancellationToken cancellationToken = default) where T : IRecord {
            var record = await Table.GetItemAsync(item.PK, item.SK, cancellationToken);
            return (record != null)
                ? JsonSerializer.Deserialize<T>(record.ToJson())
                : default;
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
            tasks.Add(Table.DeleteItemAsync(item.PK, item.SK));

            // delete projections
            if(item is IProjectedRecord<T> projectedItem) {
                foreach(var projection in projectedItem.Projections) {
                    tasks.Add(Table.DeleteItemAsync(projection.GetPK(item), projection.GetSK(item), cancellationToken));
                }
            }
            return Task.WhenAll(tasks);
        }

        public Task<IEnumerable<S>> FindRelatedAsync<P,S>(P item, CancellationToken cancellationToken = default)
            where P : IRecord
            where S : IRecord, new()
            => DoSearchAsync<S>(Table.Query(item.PK, new QueryFilter("SK", QueryOperator.BeginsWith, new S().SK)), cancellationToken);

        private Task PutItemAsync<T>(T item, PutItemOperationConfig config, CancellationToken cancellationToken = default) where T : IRecord {
            var json = JsonSerializer.Serialize(item);
            var tasks = new List<Task> {

                // store record
                PutAsync(item.PK, item.SK)
            };

            // store all projections of the record
            if(item is IProjectedRecord<T> projectedItem) {
                tasks.AddRange(projectedItem.Projections.Select(projection => PutAsync(projection.GetPK(item), projection.GetSK(item))));
            }
            return Task.WhenAll(tasks);

            // local functions
            Task PutAsync(string pk, string sk) {
                var document = Document.FromJson(json);
                document["PK"] = pk;
                document["SK"] = sk;
                return Table.PutItemAsync(document, config, cancellationToken);
            }
        }
    }
}
