/*
 * MindTouch λ#
 * Copyright (C) 2018-2019 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit mindtouch.com;
 * please review the licensing section.
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

using Newtonsoft.Json;

namespace LambdaSharp.Demo.WebSocketsChat.Common {

    public abstract class AMessageRequest {

        //--- Properties ---
        [JsonProperty("action", Required = Required.Always)]
        public string Action { get; set; }
    }

    public class SendMessageRequest : AMessageRequest {

        //--- Properties ---
        [JsonProperty("text", Required = Required.Always)]
        public string Text { get; set; }

        public int? Year { get; set; }

    }

    public class UserMessageResponse {

        //--- Properties ---
        [JsonProperty("action", Required = Required.Always)]
        public string Action { get; } = "message";

        [JsonProperty("from", Required = Required.Always)]
        public string From { get; set; }

        [JsonProperty("text", Required = Required.Always)]
        public string Text { get; set; }
    }

    public class NotifyMessage {

        //--- Properties ---
        [JsonProperty(Required = Required.Always)]
        public string Message { get; set; }
    }
}
