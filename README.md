# SharpRpc for C#

[![NuGet](https://img.shields.io/nuget/v/SharpRpc.Core.svg)](https://www.nuget.org/packages/SharpRpc.Core)
[![NuGet](https://img.shields.io/nuget/vpre/SharpRpc.Core.svg)](https://www.nuget.org/packages/SharpRpc.Core)

Fast and efficient remote procedure call (RPC) framework for C#

## Table of Contents

  - [Features](#features)
  - [Installation](#installation)
  - [Code generation](#code-generation)

## Features

  * High throughput capability (especialy while dealing with lot of small messages)
  * Low latency
  * Direct and backward calls (callbacks) in one contract
  * Optimization for message multicasting 
  * Support of grpc-like streams
  * Configurable back-pressure
 
## Installation
  
This library is distributed via NuGet. We target .NET 4.71 and .NET 5.0. The library code is pure C#.
  
In order to get things working, you need you need to install the following NuGet packages.

Core library:

```ps1
Install-Package SharpRpc.Core
```

Builder library with a [code generator](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) to generate messages and stubs for your RPC contract:

```ps1
Install-Package SharpRpc.Builder
```

And a serializer library: 

```ps1
Install-Package MessagePack
```
Note: Only MessagePack is supported by now. More serializers will be added in near future...

## Contract

  To define a contract you need to create a public interface with RpcContract and RpcSerializer attributes.

```csharp
[RpcContract]
[RpcSerializer(SerializerChoice.MessagePack)]
public class MyContract
{
	[Rpc(RpcType.Call)]
	int MyRemoteCall(FooEntity entity, int p1);

	[Rpc(RpcType.Message)]
	void MyRemoteMessage(int p1, string p2);
}
```

Each remote call must be marked with Rpc attribute with one of four call types:

| Rpc Type | Call Initiator | Description |
| --- | --- | --- |
| Call | Client | Request from cleint to server. |
| Message |  Client | One-way message from client to server. |
| Callback | Server | Request from server to client. |
| CallbackMessage | Server | One-way message from server to client. |

If you have objects as call parameters or results, they must be attributed according to specification of serializer you use. For example (MessagePack):

```csharp
[MessagePackObject]
public class FooEntity
{
    [Key(0)]
    public string Name { get; set; }

    [Key(1)]
    public int Age { get; set; }

    [Key(2)]
    public double Height { get; set; }
}
```

## Code generation

For each contract class the builder generates corresponding wrapper class which contains several subclasses to facilitate RPC communication.
The builder composes class name from contract name by adding '_Gen' suffix to it.

| Class name | Description |  
| --- | --- |
| <contract_name>_Gen.Client |  |
| <contract_name>_Gen.ServiceBase |  |
| <contract_name>_Gen.CallbackClient |  |
| <contract_name>_Gen.CallbackServiceBase |  |
