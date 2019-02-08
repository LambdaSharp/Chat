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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LambdaSharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaSharp.Sample.WebsocketsChat.OnConnectFunction {

    public class Function : ALambdaFunction<APIGatewayProxyRequest, APIGatewayProxyResponse> {

        //--- Fields ---
        private string _tableName;
        private IAmazonDynamoDB _dynamoDbClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _tableName = config.ReadDynamoDBTableName("ConnectionsTable");
            _dynamoDbClient = new AmazonDynamoDBClient();
        }

        public override async Task<APIGatewayProxyResponse> ProcessMessageAsync(APIGatewayProxyRequest request, ILambdaContext context) {
            try {
                LogInfo($"ConnectionId: {request.RequestContext.ConnectionId}");
                await _dynamoDbClient.PutItemAsync(new PutItemRequest {
                    TableName = _tableName,
                    Item = new Dictionary<string, AttributeValue> {
                        ["ConnectionId"] = new AttributeValue {
                            S = request.RequestContext.ConnectionId
                        }
                    }
                });
                return new APIGatewayProxyResponse {
                    StatusCode = 200,
                    Body = "Connected."
                };
            } catch(Exception e) {
                LogError(e);
                return new APIGatewayProxyResponse {
                    StatusCode = 500,
                    Body = $"Failed to connect: {e.Message}"
                };
            }
        }
    }
}
