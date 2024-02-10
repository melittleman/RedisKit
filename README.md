# RedisKit
> A .NET Standard 2.1 helper library for common Redis client functionality. This project is very much still a work in progress.

This package aims to build upon [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) and [NRedisStack](https://github.com/redis/NRedisStack) in order to provide all the functionality needed to get up and running with a new project utilizing Redis as fast as possible. 

With **RedisKit** you can add multiple named Redis connections within the Dependency Injection container _(beneficial in hybrid environments where Redis server setups could be different e.g. persistent vs non-persistent)_, then configure each of these connections with features such as .NET Data Protection keys persistence, an `ITicketStore` implementation to store large Claims Principals from your application's authentication cookies _(very useful within Blazor Server applications where no HTTP Context is available)_, individual health checks to each Redis server, message consumer and producer implementations to make working with Redis Streams more simple as well as many other helpful methods and extensions for working with the [RedisJSON](https://redis.io/docs/data-types/json) and [RediSearch](https://github.com/RediSearch/RediSearch) modules.

[![Build & Test](https://github.com/melittleman/RedisKit/actions/workflows/build-test.yml/badge.svg)](https://github.com/melittleman/RedisKit/actions/workflows/build-test.yml)

## Contents
- [Getting Started](#getting-started)
- [Repository Structure](#repository-structure)
- [Usage](#usage)
    * [Adding A Named Connection](#adding-a-named-connection)
    * [Using A Named Connection](#using-a-named-connection)
    * [Add Multiple Connections](#add-multiple-connections)
    * [Building A Connection](#building-a-connection)
        - [JSON Documents](#json-documents)
        - [Messaging](#Messaging)
        - [Data Protection](#data-protection)
        - [Authentication](#authentication)
        - [Health Checks](#health-checks)

## Getting Started
To get started with this library, you will first need to download and install either the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or Microsoft [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/) which comes bundled with the SDK.

## Repository Structure
- **.github** - Files needed by GitHub are contained in the `.github` directory. For example; the `workflows` directory contains any _Build_, _Test_ or _Package_ configurations needed by **GitHub** [**Actions**](https://github.com/melittleman/RedisKit/actions).
- **src** - The _Source_ directory contains all the source code for the library.
- **test** - Contains all unit and integration tests relating to the **src** directory. These tests are implemented in [XUnit](https://xunit.net/) and can be run using the ```dotnet test``` CLI. These tests will also be run as part of the [Build & Test](https://github.com/melittleman/RedisKit/actions/workflows/build-test.yml) workflow in GitHub Actions.

## Usage
Once you have installed the desired version of **RedisKit** you can configure this during service registration to be used by an application in the following ways.

### Adding A Named Connection
You can add a named Redis connection to the Dependency Injection container and configure it by passing in an `Action` of `RedisConnectionOptions`.

```csharp
using RedisKit.DependencyInjection.Extensions;

services.AddRedisConnection("cloud-cache", options =>
{
    options.ClientName = "MyCompany.MyProduct";
    options.ConnectionString = "redislabs.com:6379,allowAdmin=true,ssl=false";
});
```

The `ConnectionString` property can be provided in either of two ways:
1. Using the `StackExchange.Redis` format with a comma-separated list of arguments as outlined [here](https://stackexchange.github.io/StackExchange.Redis/Configuration.html#basic-configuration-strings). 
For example: `redis0:6379,redis1:6380,allowAdmin=true,ssl=false`
2. Using the Redis CLI URI style with the `redis://` protocol prefix. For example: `redis://username:password@redislabs.com:1234`

### Using A Named Connection

This can then be used later anywhere in the application via the Singleton `IRedisConnectionProvider` that retrieves named Redis connections.
```csharp
using RedisKit;
using RedisKit.DependencyInjection.Abstractions;

private readonly IRedisConnection _redis;

public MyClassConstructor(IRedisConnectionProvider provider)
{
    _redis = provider.GetRequiredConnection("cloud-cache");
}

public async Task DoSomethingAsync()
{
    await _redis.Db.StringSetAsync("key", "value");
}
```

If only a single Redis connection is being used within the application, this can then easily be retrieved directly from DI, rather than going via the provider.
```csharp
using RedisKit.Abstractions;

private readonly IRedisConnection _redis;

public MyClassConstructor(IRedisConnection redis)
{
    _redis = redis
}

public Task<T> GetSomethingAsync()
{
    return _redis.Db.HashGetAsync<T>("key");
}
```

You can continue to use a connection in exactly the same ways that you would otherwise use [NRedisStack](https://github.com/redis/NRedisStack) or [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) directly.
```csharp
using StackExchange.Redis;

using NRedisStack;
using NRedisStack.RedisStackCommands;

using RedisKit.DependencyInjection.Abstractions;

private readonly IRedisConnection _redis;

public MyClassConstructor(IRedisConnectionProvider provider)
{
    _redis = provider.GetRequiredConnection("enterprise-db");
}

public async Task<bool> ExpireKeyAsync()
{
    IDatabase db = _redis.Db;

    return await db.KeyExpireAsync("key", TimeSpan.FromMinutes(5));
}

public async Task<double> IncrementNumberAsync() 
{
    JsonCommands json = _redis.Json;

    return await json.NumberIncrbyAsync("key", "$.number", 1.2);
}
```

### Add Multiple Connections
The main benefit of configuring these named connections is to be able to connect to multiple Redis servers from a single application when the requirements of that connection differ.
For example; a caching instance that does not have persistence or high availability enabled, and therefore reduces costs. And a Messaging instance, where both persistence and AOF writing may be needed.

```csharp
using RedisKit.DependencyInjection.Extensions;

services.AddRedisConnection("enterprise-cache", options =>
{
    options.ClientName = "MyCompany.MyApplication.Caching";
    options.ConnectionString = "redislabs.com:18526,allowAdmin=false,ssl=true";
});

services.AddRedisConnection("onprem-message-broker", options =>
{
    options.ClientName = "MyProduct.Messaging";
    options.ConnectionString = "redis://username:password@localhost:6379";
});
```

These can then individually both be retrieved and used in the same way as detailed above in the [Using A Named Connection](#using-a-named-connection) section.

### Building A Connection
The return type of `IServiceCollection.AddRedisConnection()` is a new instance of `IRedisConnectionBuilder` which can be used to chain further configuration onto this connection via it's _Fluent_ API.
For example, to add the .NET Data Protection middleware to the application that utilizes your named Redis connection:

```csharp
using RedisKit.DependencyInjection.Extensions;

services
    .AddRedisConnection("elasticache", options...)
    .AddRedisDataProtection();
```

#### JSON Documents
**RedisKit** is also able to help abstract Redis JSON document usage within the application. You can use your own custom JSON converters for (de)serialization by configuring it in the following way.

```csharp
using RedisKit.DependencyInjection.Extensions;

services.ConfigureRedisJson(options =>
{
    options.Serializer.Converters.Add(new MyCustomJsonConverter());
});
```

Then pass the `RedisJsonOptions` into the available `JsonCommands` methods. 
```csharp
using RedisKit.DependencyInjection.Abstractions;

private readonly JsonCommands _json;
private readonly RedisJsonOptions _options;

public MyClassConstructor(IRedisConnectionProvider provider, IOptions<RedisJsonOptions> options)
{
    IRedisConnection redis = provider.GetRequiredConnection("document-db");

    _json = redis.Json;
    _options = options.Value;
}

public Task<MyCustomClass> GetSomethingAsync()
{
    return _json.GetAsync<MyCustomClass>("key", _options);
}
```

#### Messaging
There are `IMessageConsumer<T>` and `IMessageProducer<T>` abstractions available for interacting with Redis as a message broker. 
This utilizes the **Redis Streams** data type on the server and is implemented in the `RedisStreamsConsumer<TMessage>` and `RedisStreamsProducer<TMessage>` services respectively.

In order to get started simply chain either one or both of the methods below during service registration.
> **Note**: These do not need to be chained to the same connection. For example you could _Consume_ messages from an internal Redis server and then _Produce_ these out to an entirely different public server.

```csharp
using RedisKit.DependencyInjection.Extensions;

services
    .AddRedisConnection("message-broker", options...)
    .AddRedisStreamsConsumer(options =>
    {
        options.ConsumerGroupName = "Project.ClientApp.Consumer";

    }).AddRedisStreamsProducer();
```

You can then request these abstractions from the Dependency Injection container and use them to produce or consume messages.

```csharp
using RedisKit.Messaging.Abstractions;

private readonly IMessageConsumer<MyMessage> _consumer;
private readonly IMessageProducer<MyMessage> _producer;

public MyMessagingClass(IMessageConsumer<MyMessage> consumer, IMessageProducer<MyMessage> producer)
{
    _consumer = consumer;
    _producer = producer
}

public Task ConsumeSomethingAsync()
{
    MessageResult<ICollection<MyMessage>> results = await _consumer.ConsumeAsync(5);

    // omitted for brevity
}

public Task ProduceSomethingAsync(MyMessage message)
{
    bool success = await _producer.SendAsync("key", message)

    // omitted for brevity
}
```

> **Note**: Unfortunately there is a limitation in the `StackExchange.Redis` library that prevents blocking reads, due to the fact that all commands leverage a single `ConnectionMultiplexer` instance. 
> Therefore, message consumers will need to periodically request messages as appropriate. See more informaion [here](https://developer.redis.com/develop/dotnet/streams/blocking-reads).

#### Data Protection
The .NET Data Protection providers can be configured to use your named Redis connection and save the keys under a specified location like the following:

```csharp
using RedisKit.DependencyInjection.Extensions;

services
    .AddRedisConnection("redis-persistent", options...)
    .AddRedisDataProtection(options =>
    {
        options.KeyName = "my-application:data-protection:keys";
    });
```

#### Authentication
If using cookie authentication, there is a provided implementation of `ITicketStore` that can be configured on `CookieAuthenticationOptions` in order
to utilize the named Redis connection to store instances of `AuthenticationTicket` within the server as JSON. This then easily allows for distributed
authentication sessions, and removes the reliance on browsers storing very large `ClaimsPrincipal` payloads and from purging this data when they are expected to.

> **Note**: You MUST ensure that [Data Protection](#data-protection) has been configured for this to work correctly.

```csharp
using RedisKit.DependencyInjection.Extensions;
using RedisKit.Json.Converters;

services
    .AddRedisConnection("redis-persistent", options...)
    .AddRedisDataProtection(options...)
    .AddRedisTicketStore(options =>
    {
        options.KeyPrefix = "client-app:authentication:tickets:";
        options.CookieSchemeName = "My Cookie Scheme";
    });
```

#### Health Checks
You can configure the built in .NET Health Check framework to test connectivity to your named Redis connection as part of it's configured Health Checks.

```csharp
using RedisKit.DependencyInjection.Extensions;

services
    .AddRedisConnection("redis-server", options...)
    .AddHealthCheck();
```
