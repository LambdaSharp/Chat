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
using Demo.WebSocketsChat.Common.DynamoDB;

namespace Demo.WebSocketsChat.Common.Records {

    public abstract class ARecord : IRecord {

        //--- Constants ---
        public const string USER_PREFIX = "USER#";
        public const string CHANNEL_PREFIX = "ROOM#";
        public const string CONNECTION_PREFIX = "WS#";
        public const string TIMESTAMP_PREFIX = "WHEN#";
        public const string INFO = "INFO";

        //--- Class Methods ---
        protected static IEnumerable<IProjection<T>> Projections<T>(params (Func<T, string> GetPK, Func<T, string> GetSK)[] projectors) where T : IRecord
            => projectors.Select(tuple => (IProjection<T>)new Projection<T>(tuple.GetPK, tuple.GetSK)).ToArray();

        protected static string GetRandomString(int length) => DynamoTable.GetRandomString(length);

        //--- Abstract Properties ---
        public abstract string PK { get; }
        public abstract string SK { get; }

        //--- Propterties ---
        public string _Type => GetType().Name;
    }
}
