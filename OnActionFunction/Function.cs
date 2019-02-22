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
using LambdaSharp.Demo.WebSocketsChat.Common;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaSharp.Demo.WebSocketsChat.OnActionFunction {

    public class Message {

        //--- Properties ---
        [JsonProperty("type")]
        public string Type { get; set; } = "message";

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class BadRequestException : Exception {

        //--- Fields ---
        public readonly string Reason;

        //--- Constructors ---
        public BadRequestException(string reason) {
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }
    }

    public class Function : ALambdaFunction<APIGatewayProxyRequest, APIGatewayProxyResponse> {

        //--- Fields ---
        private ConnectionsTable _connections;
        private IAmazonApiGatewayManagementApi _apiClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _connections = new ConnectionsTable(
                config.ReadDynamoDBTableName("ConnectionsTable"),
                new AmazonDynamoDBClient()
            );
        }

        public override async Task<APIGatewayProxyResponse> ProcessMessageAsync(APIGatewayProxyRequest apiProxyRequest, ILambdaContext context) {
            try {
                LogInfo($"Action: {apiProxyRequest.RequestContext.ConnectionId} [{apiProxyRequest.RequestContext.RouteKey}]");

                // initialize API Gateway management client
                var endpoint = $"https://{apiProxyRequest.RequestContext.DomainName}/{apiProxyRequest.RequestContext.Stage}";
                LogInfo($"API Gateway management endpoint: {endpoint}");
                _apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig {
                    ServiceURL = endpoint
                });

                // deserialize request
                var message = DeserializeJson<Message>(apiProxyRequest.Body);

                // dispatch request based on action
                string response;
                switch(message.Type) {
                case "message":
                    response = await HandleMessageAsync(message.From, message.Text);
                    break;
                case null:
                    throw new BadRequestException("missing action");
                default:
                    throw new BadRequestException($"unknown action '{message.Type}'");
                }
                return new APIGatewayProxyResponse {
                    StatusCode = 200,
                    Body = response
                };
            } catch(BadRequestException e) {
                LogWarn("Bad request: {0}", e.Reason);
                return new APIGatewayProxyResponse {
                    StatusCode = 400,
                    Body = $"Bad Request: {e.Reason}"
                };
            } catch(Exception e) {
                LogError(e);
                return new APIGatewayProxyResponse {
                    StatusCode = 500,
                    Body = $"Request failed: {e.Message}"
                };
            }
        }

        private async Task<string> HandleMessageAsync(string from, string message) {

            // enumerate open connections
            var connections = await _connections.GetAllRowsAsync();
            LogInfo($"Found {connections.Count()} open connection(s)");

            // attempt to send message on all open connections
            var messageBytes = Encoding.UTF8.GetBytes(SerializeJson(new Message {
                From = from,
                Text = message
            }));
            var outcomes = await Task.WhenAll(
                connections
                    .Select(async (connectionId, index) => {
                        LogInfo($"Post to connection {index}: {connectionId}");
                        try {
                            await _apiClient.PostToConnectionAsync(new PostToConnectionRequest {
                                ConnectionId = connectionId,
                                Data = new MemoryStream(messageBytes)
                            });
                            return true;
                        } catch(AmazonServiceException e) {

                            // API Gateway returns a status of 410 GONE when the connection is no
                            // longer available. If this happens, we simply delete the identifier
                            // from our DynamoDB table.
                            if(e.StatusCode == HttpStatusCode.Gone) {
                                LogInfo($"Deleting gone connection: {connectionId}");
                                await _connections.DeleteRowAsync(connectionId);
                            } else {
                                LogError(e, "PostToConnectionAsync() failed");
                            }
                            return false;
                        }
                    })
            );
            return $"Data sent to {outcomes.Count(result => result)} connections";
        }
    }
}
