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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using LambdaSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaSharp.Sample.WebsocketsChat.SendMessageFunction {

    public class Function : ALambdaFunction<APIGatewayProxyRequest, APIGatewayProxyResponse> {

        //--- Fields ---
        private string _tableName;
        private IAmazonDynamoDB _dynamoDbClient = new AmazonDynamoDBClient();

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _tableName = config.ReadDynamoDBTableName("ConnectionsTable");
            _dynamoDbClient = new AmazonDynamoDBClient();
        }

        public override async Task<APIGatewayProxyResponse> ProcessMessageAsync(APIGatewayProxyRequest request, ILambdaContext context) {
            try {

                // deserialize message from request body
                var message = JsonConvert.DeserializeObject<JObject>(request.Body);
                var data = message["data"]?.ToString();
                var bytes = UTF8Encoding.UTF8.GetBytes(data);

                // initialize API Gateway management client
                var endpoint = $"https://{request.RequestContext.DomainName}/{request.RequestContext.Stage}";
                LogInfo($"API Gateway management endpoint: {endpoint}");
                var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
                    ServiceURL = endpoint
                });

                // enumerate open connections
                var connections = (await _dynamoDbClient.ScanAsync(new ScanRequest {
                        TableName = _tableName,
                        ProjectionExpression = "ConnectionId"
                    }))
                    .Items
                    .Select(item => item["ConnectionId"].S)
                    .ToList();
                LogInfo($"Found {connections.Count} open connection(s)");

                // attempt to send message on all open connections
                var outcomes = await Task.WhenAll(connections
                    .Select(async (connectionId, index) => {
                        try {
                            LogInfo($"Post to connection {index}: {connectionId}");
                            await apiClient.PostToConnectionAsync(new PostToConnectionRequest {
                                ConnectionId = connectionId,
                                Data = new MemoryStream(bytes)
                            });
                            return true;
                        } catch(AmazonServiceException e) {

                            // API Gateway returns a status of 410 GONE when the connection is no
                            // longer available. If this happens, we simply delete the identifier
                            // from our DynamoDB table.
                            if(e.StatusCode == HttpStatusCode.Gone) {
                                LogInfo($"Deleting gone connection: {connectionId}");
                                await _dynamoDbClient.DeleteItemAsync(new DeleteItemRequest {
                                    TableName = _tableName,
                                    Key = new Dictionary<string, AttributeValue> {
                                        ["ConnectionId"] = new AttributeValue {
                                            S = connectionId
                                        }
                                    }
                                });
                            } else {
                                LogError(e, "unable to delete gone connection");
                            }
                            return false;
                        }
                    })
                );
                return new APIGatewayProxyResponse {
                    StatusCode = 200,
                    Body = $"Data send to {outcomes.Count(result => result)} connections"
                };
            } catch(Exception e) {
                LogError(e);
                return new APIGatewayProxyResponse {
                    StatusCode = 500,
                    Body = $"Failed to send message: {e.Message}"
                };
            }
        }
    }
}
