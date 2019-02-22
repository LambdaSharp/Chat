# λ# - Create a Web Chat with API Gateway Websockets

[This sample requires the λ# tool to deploy.](https://github.com/LambdaSharp/LambdaSharpTool)

## Overview

This λ# module creates a web chat front-end and back-end using CloudFormation. The front-end is served by an S3 bucket and secured by a Cloudfront distribution. The back-end uses API Gateway Websockets to facilitate communication between clients. The assets for the front-end are uploaded from the `wwwroot` folder and copied to the S3 bucket during deployment. Afterwards, a Cloudfront distribution is created to provide secure access over `https://` to the front-end. In addition, an API Gateway (v2) is deployed with three Lambda functions that handle websocket connections.

## Deploy

This module is compiled to CloudFormation and deployed using the λ# CLI.
```
git clone https://github.com/LambdaSharp/WebSocketsChat-Sample.git
cd WebSocketsChat-Sample
lash deploy
```

## Details

1. Create a DynamoDB table to track open connections.
1. Deploy the `OnConnectFunction` and `OnDisconnectFunction` to manage websocket connections.
1. Deploy `OnActionFunction` to broadcast messages to all open connections.
1. Create an S3 bucket configured to host a website.
1. Create a bucket policy to allow for public access.
1. Create a `config.json` file with the websocket URL.
1. Copy the `wwwroot` files to the S3 bucket.
1. Create Cloudfront distribution to enable https:// access to the S3-hosted website _(NOTE: this can take 20 minutes to deploy!)_
1. Show the website URL.
1. Show the websocket URL.

## Other Resources

The following site allows direct interactions with the websockets end-point using the websocket URL.

https://www.websocket.org/echo.html

The websocket payload is a JSON document with the following format:
```json
{
    "type": "message",
    "from": "<username>",
    "text": "<message>"
}
```

## Acknowledgements

This λ# module is a port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

Inspiration for the chat logic was taken from [Node.js & WebSocket — Simple chat tutorial](https://medium.com/@martin.sikora/node-js-websocket-simple-chat-tutorial-2def3a841b61).

## License

_Apache 2.0_ for the module and code.
