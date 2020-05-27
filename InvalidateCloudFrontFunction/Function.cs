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
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Lambda.S3Events;
using LambdaSharp;

namespace Demo.WebSocketsChat.InvalidateCloudFrontFunction {

    public class Function : ALambdaFunction<S3Event, string> {

        //--- Fields ---
        private IAmazonCloudFront _cloudfrontClient;
        private string _distributionId;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _distributionId = config.ReadText("WebsiteCloudFront");
            _cloudfrontClient = new AmazonCloudFrontClient();
        }

        public override async Task<string> ProcessMessageAsync(S3Event request) {
            var paths = request.Records.Select(record => "/" + record.S3.Object.Key).ToList();
            await _cloudfrontClient.CreateInvalidationAsync(new CreateInvalidationRequest {
                DistributionId = _distributionId,
                InvalidationBatch = new InvalidationBatch {
                    CallerReference = request.Records.First().ResponseElements.XAmzRequestId,
                    Paths = new Paths {
                        Items = paths,
                        Quantity = paths.Count
                    }
                }
            });
            LogInfo($"invalidated {paths.Count:N0} CloudFront paths:\n{string.Join("\n", paths)}");
            return "Ok";
        }
    }
}
