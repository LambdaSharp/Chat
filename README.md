# λ# - Use WebSockets with API Gateway

[This sample requires the λ# tool to deploy.](https://github.com/LambdaSharp/LambdaSharpTool)

## Overview

This λ# module is a port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

## Deploy

This module is compiled to CloudFormation and deployed using the λ# CLI.
```
git clone https://github.com/LambdaSharp/WebSockets-Sample.git
cd WebSockets-Sample
lash deploy
```

## Details

1. Create a DynamoDB table to track open connections.
1. Deploy the `OnConnectFunction` and `OnDisconnectFunction` to manage websocket connections.
1. Deploy `OnActionFunction` to broadcast messages to all open connections.
1. Show the websocket URL.

## License

_Apache 2.0_ for the module and code.
