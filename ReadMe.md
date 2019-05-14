# λ# - Create a Web Chat with API Gateway Websockets

[This sample requires the λ# tool to deploy.](https://lambdasharp.net/)

## Overview

This λ# module creates a web chat front-end and back-end using CloudFormation. The front-end is served by an S3 bucket and secured by a CloudFront distribution. The back-end uses API Gateway Websockets to facilitate communication between clients. The assets for the front-end are uploaded from the `wwwroot` folder and copied to the S3 bucket during deployment. Afterwards, a CloudFront distribution is created to provide secure access over `https://` to the front-end. In addition, an API Gateway (v2) is deployed with two Lambda functions that handle websocket connections and message notifications.

## Deploy Module

This module is compiled to CloudFormation and deployed using the λ# CLI.
```
git clone https://github.com/LambdaSharp/WebSocketsChat-Sample.git
cd WebSocketsChat-Sample
lash deploy
```

## API Gateway .NET

During the build phase, λ# extracts the message schema from the .NET implementation and uses it to configure the API Gateway V2 instance. If an incoming does not confirm to the expected schema of the web-socket route, then API Gateway will automatically reject it before it reaches the Lambda function.

```yaml
- Function: ChatFunction
  Description: Handle web-socket messages
  Memory: 256
  Timeout: 30
  Sources:

    - WebSocket: $connect
      Invoke: OpenConnectionAsync

    - WebSocket: $disconnect
      Invoke: CloseConnectionAsync

    - WebSocket: send
      Invoke: SendMessageAsync
```

Defining the JSON schema for the web-socket route doesn't require any special effort beyond some standard JSON annotations using the corresponding type.
```csharp
public abstract class AMessageRequest {

    //--- Properties ---
    [JsonProperty("action"), JsonRequired]
    public string Action { get; set; }
}

public class SendMessageRequest : AMessageRequest {

    //--- Properties ---
    [JsonProperty("text"), JsonRequired]
    public string Text { get; set; }
}

public async Task SendMessageAsync(SendMessageRequest request) {
  ...
}
```

## CloudFormation Details

The following happens when the module is deployed.

1. Create a DynamoDB table to track open connections.
1. Deploy the `ChatFunction` to handle web-socket requests.
1. Deploy `NotifyFunction` to broadcast messages to all open connections.
1. Create an S3 bucket configured to host a website.
1. Create a bucket policy to allow for public access.
1. Create a `config.json` file with the websocket URL.
1. Copy the `wwwroot` files to the S3 bucket.
1. Create CloudFront distribution to enable https:// access to the S3-hosted website _(NOTE: this can take 20 minutes to deploy!)_
1. Create an SQS queue to web-socket notifications.
1. Show the website URL.
1. Show the websocket URL.

## Other Resources

The following site allows direct interactions with the websockets end-point using the websocket URL.

https://www.websocket.org/echo.html

The websocket payload is a JSON document with the following format:
```json
{
    "action": "send",
    "text": "<message>"
}
```

## Acknowledgements

This λ# module is a port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

Inspiration for the chat logic was taken from [Node.js & WebSocket — Simple chat tutorial](https://medium.com/@martin.sikora/node-js-websocket-simple-chat-tutorial-2def3a841b61).

## License

_Apache 2.0_ for the module and code.
