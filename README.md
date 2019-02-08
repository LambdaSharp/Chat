# λ# Port of simple-websockets-chat-app

This is a λ# port of the [netcore-simple-websockets-chat-app](https://github.com/normj/netcore-simple-websockets-chat-app) sample for AWS Lambda. For more information [Announcing WebSocket APIs in Amazon API Gateway](https://aws.amazon.com/blogs/compute/announcing-websocket-apis-in-amazon-api-gateway/) blog post.

## Deploy


To deploy this sample use the the [λ# CLI](https://github.com/LambdaSharp/LambdaSharpTool).
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
