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
using Demo.WebSocketsChat.Common.DynamoDB;

namespace Demo.WebSocketsChat.Common.Records {

    public sealed class SubscriptionRecord :
        ARecord,
        IRecordProjected<SubscriptionRecord>,
        ISecondaryRecord<ChannelRecord>,
        ISecondaryRecord<UserRecord>
    {

        //--- Properties ---
        public override string PK => CHANNEL_PREFIX + ChannelId;
        public override string SK => USER_PREFIX + UserId;
        public string ChannelId { get; set; }
        public string UserId { get; set; }
        public long LastSeenTimestamp { get; set; }

        //--- IProjectedRecord<ConnectionRecord> Members ---
        IEnumerable<IProjection<SubscriptionRecord>> IRecordProjected<SubscriptionRecord>.Projections
            => Projections<SubscriptionRecord>((item => USER_PREFIX + UserId, item => CHANNEL_PREFIX + ChannelId));

        //--- ISecondaryRecord<ChannelRecord> Members ---
        string ISecondaryRecord<ChannelRecord>.SKPrefix => USER_PREFIX;

        //--- IProjectedRecord<UserRecord> Members ---
        string ISecondaryRecord<UserRecord>.SKPrefix => CHANNEL_PREFIX;
    }
}
