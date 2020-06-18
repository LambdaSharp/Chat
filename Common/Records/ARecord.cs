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
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;

namespace Demo.WebSocketsChat.Common.Records {

    public abstract class ARecord {

        //--- Constants ---
        public const string USER_PREFIX = "USER#";
        public const string CHANNEL_PREFIX = "ROOM#";
        public const string CONNECTION_PREFIX = "WS#";
        public const string TIMESTAMP_PREFIX = "WHEN#";
        public const string INFO = "INFO";

        //--- Class Fields ---
        private readonly static Random _random = new Random();

        //--- Class Methods ---
        protected async static Task<IEnumerable<T>> DoSearchAsync<T>(Search search) {
            var results = new List<T>();
            do {
                var documents = await search.GetNextSetAsync();
                results.AddRange(documents.Select(document => JsonConvert.DeserializeObject<T>(document.ToJson())));
            } while(!search.IsDone);
            return results;
        }

        protected async static Task<T> GetItemAsync<T>(Table table, string pk, string sk) {
            var record = await table.GetItemAsync(pk, sk);
            return (record != null)
                ? JsonConvert.DeserializeObject<T>(record.ToJson())
                : default;
        }

        protected static string RandomString(int length)
            => new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(chars => chars[_random.Next(chars.Length)]).ToArray());

        //--- Abstract Properties ---
        public abstract string PK { get; }
        public abstract string SK { get; }

        //--- Methods ---
        public virtual Task CreateOrUpdateAsync(Table table)
            => table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(this)));

        // TODO: ensure that the record does NOT exist yet
        public virtual Task CreateAsync(Table table)
            => table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(this)));

        // TODO: ensure that the record already exists
        public virtual Task UpdateAsync(Table table)
            => table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(this)));

        public virtual Task DeleteAsync(Table table)
            => table.DeleteItemAsync(PK, SK);
    }
}
