# λ# Port of simple-websockets-chat-app

This is a λ# port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

## Deploy Module

To deploy this sample module use the the [λ# CLI](https://github.com/LambdaSharp/LambdaSharpTool).
```
dotnet tool install -g LambdaSharp.Tool --version 0.5-RC2
```

Initialize the λ# CLI
```
lash config
lash init --tier Sandbox
```

Then to deploy execute the following command in the root directory of this repository.
```
lash deploy --tier Sandbox
```

## Module Definition

```yaml
Module: LambdaSharp.Sample.WebsocketsChat
Items:

  - Resource: ConnectionsTable
    Scope: "*"
    Type: AWS::DynamoDB::Table
    Allow: ReadWrite
    Properties:
      BillingMode: PAY_PER_REQUEST
      AttributeDefinitions:
        - AttributeName: ConnectionId
          AttributeType: S
      KeySchema:
        - AttributeName: ConnectionId
          KeyType: HASH

  - Resource: ApiGatewayPermissions
    Allow: execute-api:ManageConnections
    Value: arn:aws:execute-api:*:*:*/@connections/*

  - Function: OnDisconnectFunction
    Memory: 256
    Timeout: 30

  - Function: OnConnectFunction
    Memory: 256
    Timeout: 30

  - Function: SendMessageFunction
    Memory: 256
    Timeout: 30
```
