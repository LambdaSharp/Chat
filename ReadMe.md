# LambdaSharp - Create a Web Chat with API Gateway WebSockets and ASP.NET Core Blazor WebAssembly

[This sample requires the LambdaSharp CLI to deploy.](https://lambdasharp.net/)

## Overview

This LambdaSharp module creates a web chat front-end using [ASP.NET Core Blazor WebAssembly](https://docs.microsoft.com/en-us/aspnet/core/blazor/get-started) and back-end using [API Gateway V2 WebSocket](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) as self-contained CloudFormation template. The front-end is served by an S3 bucket and secured by a CloudFront distribution. The front-end code is delivered as [WebAssembly](https://webassembly.org/) using ASP.NET Core Blazor. The back-end uses API Gateway V2 WebSocket to facilitate communication between clients. The code and assets for the front-end are built by `dotnet` and then copied to the S3 bucket during deployment. Afterwards, a CloudFront distribution is created to provide secure access over `https://` to the front-end. Finally, an API Gateway V2 WebSocket is deployed with two Lambda functions that handle WebSocket connections and message notifications.

> **NOTE:** This LambdaSharp module requires .NET Core 3.1.300 and LambdaSharp.Tool 0.8.0.5, or later.

![WebChat](Assets/LambdaSharpWebChat.png)

## Deploy Module

This module is compiled to CloudFormation and deployed using the LambdaSharp CLI.
```
git clone https://github.com/LambdaSharp/WebSocketsChat-Sample.git
cd WebSocketsChat-Sample
lash deploy
```

## API Gateway .NET (WebSocket)

During the build phase, LambdaSharp extracts the message schema from the .NET implementation and uses it to configure the API Gateway V2 instance. If an incoming does not confirm to the expected schema of the web-socket route, then API Gateway will automatically reject it before it reaches the Lambda function.

```yaml
- Function: ChatFunction
  Description: Handle web-socket messages
  Memory: 256
  Timeout: 30
  Sources:

    - WebSocket: $connect
      Invoke: OpenConnectionAsync

    - WebSocket: $disconnect
      Inpvoke: CloseConnectionAsync

    - WebSocket: send
      Invoke: SendMessageAsync
```

Defining the JSON schema for the web-socket route doesn't require any special effort.

```csharp
public abstract class AMessageRequest {

    //--- Properties ---
    public string Action { get; set; }
}

public class SendMessageRequest : AMessageRequest {

    //--- Constructors ---
    public SendMessageRequest() => Action = "send";

    //--- Properties ---
    public string ChannelId { get; set; }
    public string Text { get; set; }
}
```

## CloudFormation Details

The following happens when the module is deployed.

1. Create a DynamoDB table with a secondary index to store application records.
1. Deploy the `ChatFunction` to handle web-socket requests.
1. Deploy `NotifyFunction` to broadcast messages to all open connections.
1. Create a private S3 bucket.
1. Create a bucket policy to CloudFront access.
1. Create a `config.json` file with the websocket URL.
1. Copy the `wwwroot` files to the S3 bucket using [brotli compression](https://en.wikipedia.org/wiki/Brotli).
1. Create CloudFront distribution to enable https:// access to the S3-hosted website
1. Create an SQS queue to buffer web-socket notifications.
1. Show the website URL.
1. Show the websocket URL.

> **NOTE:** Creating the CloudFront distribution takes up to 5 minutes. Granting permission to CloudFront to access the private S3 bucket can take up to an hour!

## Other Resources

The following site allows direct interactions with the WebSocket end-point using the WebSocket URL.

https://www.websocket.org/echo.html

This JSON message sends a _"Hello World!"_ notification to all participants:
```json
{
    "Action": "send",
    "ChannelId": "General",
    "Text": "Hello World!"
}
```

This JSON message changes the user name to _Bob_ for the current user:
```json
{
    "Action": "rename",
    "UserName": "Bob"
}
```

## Login Flow

1. Show splash screen in `index.html`
1. Continue showing the same splash screen when `Index.razor` loads
1. Check if we have a JWT token stored.
  1. If we do, attempt to log in with it. (optional: check if it has expired)
  1. If login is successful, then prepare to show the full interface
1. If we don't have JWT token or we failed to login with the one we had (probably b/c it's expired), show a `Login` button
1. Button redirects to Cognito login form
1. Cognito redirects back to Blazor app with `id_token=JWT` in URI fragment
1. Store JWT in local storage
1. Log into WebSocket

## DynamoDB Table

### User Record

Every user has exactly one user record associated with them. Each user is uniquely identified by the value in the `UserId` column. The user name can be customized by the user and may not be unique across all users.

The primary index is used to resolve user records by `UserId`.

The secondary index is used to list all existing users.

|Column       |Value
|-------------|------------------
|PK           |"USER#{UserId}"
|SK           |"INFO"
|GS1PK        |"USERS"
|GS1SK        |"USER#{UserId}"
|UserId       |String
|UserName     |String


### Connection Record

A connection record is created by a new connection is opened wit a user. A user can have multiple, simultaneous connections active. Each connection is uniquely identified by the value in the `ConnectionId` column.

The primary index is used to resolve connections by `ConnectionId`.

The secondary index is used to find all open connections per `UserId`.

|Column       |Value
|-------------|------------------
|PK           |"WS#{ConnectionId}"
|SK           |"INFO"
|GS1PK        |"USER#{UserId}"
|GS1SK        |"WS#{ConnectionId}"
|ConnectionId |String
|UserId       |String

### Channel Record

The channel record is created for each channel. The `Finalizer` ensures that the `General` channel always exists by default. Each channel is uniquely identified by the value in the `ChannelId` column.

The primary index is used to resolve channels by `ChannelId`.

The secondary index is used to find all existing channels.

|Column       |Value
|-------------|------------------
|PK           |"ROOM#{ChannelId}"
|SK           |"INFO"
|GS1PK        |"CHANNELS"
|GS1SK        |"ROOM#{ChannelId}"
|ChannelId    |String
|ChannelName  |String

### Subscription Record

The subscription record is created when a user subscribes to a channel. The value in the `LastSeenTimestamp` column is the UNIX epoch timestamp in milliseconds for the last message seen by the user in the given channel.

The primary index is used for finding all users subscribed by `ChannelId`.

The secondary index is used fod finding all channels subscribed by `UserId`.

|Column           |Value
|-----------------|------------------
|PK               |"ROOM#{ChannelId}"
|SK               |"USER#{UserId}"
|GS1PK            |"USER#{UserId}"
|GS1SK            |"ROOM#{ChannelId}"
|ChannelId        |String
|UserId           |String
|LastSeenTimestamp|Number

### Message Record

The message record is created for each message sent by a user on a channel. The value in the `Timestamp` column is the UNIX epoch timestamp in milliseconds when the message was sent by the user. The value in the `Jitter` column is used to minimize risk of row conflicts, in case a user sends two messages at the same time.

|Column       |Value
|-------------|------------------
|PK           |"ROOM#{ChannelId}"
|SK           |"WHEN#{Timestamp:0000000000000000}#{Jitter}"
|UserId       |String
|ChannelId    |String
|Timestamp    |Number
|Message      |String
|Jitter       |String


## Future Improvements
- [x] Allow users to rename themselves.
- [x] Remember a user's name from a previous session using local storage.
- [x] Restrict access to S3 bucket to only allow CloudFront.
- [x] Show previous messages when a user connects.
- [x] Route WebSocket requests via CloudFront.
- [x] Allow users to create or join chat rooms.
- [x] Add UI for logging in.
- [ ] Add Cognito user pool for user management.
- [ ] Secure WebSocket so they must come through CloudFront.
- [ ] Allows users to create and join rooms.

## Acknowledgements

This LambdaSharp module is a port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

Inspiration for the chat logic was taken from [Node.js & WebSocket — Simple chat tutorial](https://medium.com/@martin.sikora/node-js-websocket-simple-chat-tutorial-2def3a841b61).

## License

_Apache 2.0_ for the module and code.
