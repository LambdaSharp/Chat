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

namespace Demo.WebSocketsChat.Common.Records {

    public sealed class MessageRecord : ARecord {


        //--- Fields ---
        private string _jitter;

        //--- Properties ---
        public override string PK => CHANNEL_PREFIX + ChannelId;
        public override string SK => TIMESTAMP_PREFIX + Timestamp.ToString("0000000000000000") + "|" + Jitter;
        public long Timestamp { get; set; }
        public string UserId { get; set; }
        public string ChannelId { get; set; }
        public string Message { get; set; }
        public string Jitter {
            get {
                if(_jitter == null) {
                    _jitter = GetRandomString(4);
                }
                return _jitter;
            }
            set => _jitter = value;
        }
    }
}
