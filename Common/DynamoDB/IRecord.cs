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

namespace Demo.WebSocketsChat.Common.DynamoDB {

    public interface IRecord {

        //--- Properties ---
        string PK { get; }
        string SK { get; }
    }

    public interface IProjectedRecord<T> where T : IRecord {

        //--- Properties ---
        IEnumerable<IProjection<T>> Projections { get; }
    }

    public interface IProjection<T> where T : IRecord {

        //--- Methods ---
        string GetPK(T item);
        string GetSK(T item);
    }

    public readonly struct Projection<T> : IProjection<T> where T : IRecord {

        //--- Fields ---
        private readonly Func<T, string> _getPK;
        private readonly Func<T, string> _getSK;

        //--- Constructors ---
        public Projection(Func<T, string> getPK, Func<T, string> getSK) {
            _getPK = getPK ?? throw new ArgumentNullException(nameof(getPK));
            _getSK = getSK ?? throw new ArgumentNullException(nameof(getSK));
        }

        //--- Methods ---
        public string GetPK(T item) => _getPK(item);
        public string GetSK(T item) => _getSK(item);
    }

    public interface IPrimaryRecord : IRecord { }

    public interface IRelatedRecord : IRecord { }
}
