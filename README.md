Ceen.Httpd
=========

A tiny and efficient CIL web server, written with `async` all the way. 

Ceen.Httpd is meant for use with high-load webservers where mod_mono or another CGI-like approach would normally be required. By hosting the entire webserver in CIL, the overhead associcated with CGI, FCGI and SCGI is removed. If the handling code relies on `async`/`await` as the server does, it can scale to a very high number of simultaneous connections.

Ceen.Httpd is also very useful for devices with limited processing power and Mono. On a device with limited resources, it would be infeasible to run a full webserver, but running a small server like Ceen.Httpd makes it a viable approach.

Key features:

  - Small codebase - easy code overview
  - SSL support - secure connections
  - Async implementation - handles high number of concurrent requests
  - No dependencies - embed where you need it
  - Built on TCP level - easy to debug
  - Basic modules included - W3C Log and static file serving
  
Optional modules:

  - REST aware - Implement custom REST logic with Ceen.Mvc
  - Routing - Optional routing module with Ceen.Mvc
  - Logins - Support for login, persistent tokens, secure passphrase storage in Ceen.Security
  - Database ORM - Basic SQLite support including table creation with Ceen.Database

Standalone version, Ceen.Httpd.Cli:

  - Listen to sockets
  - Configure through text file
  - Load binaries through AppDomain (Mono or .Net full/desktop)
  - Run external handler binary for seamless restarts (Mono, .Net full/desktop and .Net core)
  - Re-load configuration by sending SIGHUP
  - Reload enables on-the-fly updates to application logic without missing a single request

Missing features:

  - No template engine: use [T4](https://msdn.microsoft.com/en-us/library/bb126445.aspx) or your favorite
  - Full application-level logging support: use [log4net](https://logging.apache.org/log4net/)
  - Advanced database queries: use [Dapper](https://github.com/StackExchange/Dapper)

Installation
============

[Ceen.Httpd is available on NuGet](https://www.nuget.org/packages/Ceen.Httpd/):
```
PM Install-Package Ceen.Httpd
```

[Ceen.Mvc is available on NuGet](https://www.nuget.org/packages/Ceen.Mvc/):
```
PM Install-Package Ceen.Mvc
```

Example
=======

Running a webserver for static content is easy:

```csharp
using System;
using Ceen.Httpd;
using Ceen.Httpd.Handler;
using Ceen.Httpd.Logging;
using System.Net;
using System.Threading;

...

public static void Main(string[] args)
{
    var tcs = new CancellationTokenSource();
    var config = new ServerConfig()
        .AddLogger(new CLFStdOut())
        .AddRoute(new FileHandler(args.Length == 0 ? "." : args[0]));

    var task = HttpServer.ListenAsync(
        new IPEndPoint(IPAddress.Any, 8080),
        false,
        config,
        tcs.Token
    );

    Console.WriteLine("Serving files, press enter to stop ...");
    Console.ReadLine();

    tcs.Cancel(); // Request stop
    task.Wait();  // Wait for shutdown
}
```


Dynamic content can be added with a simple handler:

```csharp
using System;
using Ceen.Httpd;
using Ceen.Httpd.Handler;
using Ceen.Httpd.Logging;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
...

public class TimeOfDayHandler : IHttpModule
{
    public async Task<bool> HandleAsync(HttpRequest request, HttpResponse response)
    {
        response.SetNonCacheable();
        await response.WriteAllJsonAsync(JsonConvert.SerializeObject(new { time = DateTime.Now.TimeOfDay } ));
        return true;
    }
} 

public static void Main(string[] args)
{
    var tcs = new CancellationTokenSource();
    var config = new ServerConfig()
        .AddLogger(new CLFStdOut())
        .AddRoute("/timeofday", new TimeOfDayHandler())
        .AddRoute(new FileHandler(args.Length == 0 ? "." : args[0]));

    var task = HttpServer.ListenAsync(
        new IPEndPoint(IPAddress.Any, 8080),
        false,
        config,
        tcs.Token
    );

    Console.WriteLine("Serving files, press enter to stop ...");
    Console.ReadLine();

    tcs.Cancel(); // Request stop
    task.Wait();  // Wait for shutdown
}
```

The Model-View-Controller part can simplify building REST APIs:

```csharp
using System;
using Ceen.Httpd;
using Ceen.Httpd.Handler;
using Ceen.Httpd.Logging;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
...

[Name("api")]
public interface IAPI : IControllerPrefix { }

[Name("v1")]
public interface IApiV1 : IAPI { }

[Name("entry")]
public class ApiExampleController : Controller, IApiV1
{
    [HttpGet]
    public IResult Index(IHttpContext context)
    {
        return OK();
    }

    public IResult Index(int id)
    {
        return Html("<body>Hello!</body>");
    }

    [Route("{id}/detail")]
    public IResult Detail(int id)
    {
        return Status(HttpStatusCode.NoContent);
    }
}

public static void Main(string[] args)
{
    var tcs = new CancellationTokenSource();
    var config = new ServerConfig()
        .AddLogger(new CLFStdOut())
        .AddRoute(
            typeof(ApiExampleController)
            .Assembly //Load all types in assembly
            .ToRoute(
                new ControllerRouterConfig(
                    // Set as default controller
                    typeof(ApiExampleController)
                )
            )
        );

    var task = HttpServer.ListenAsync(
        new IPEndPoint(IPAddress.Any, 8080),
        false,
        config,
        tcs.Token
    );

    // GET "/api/v1" => ApiExampleController.Index
    // GET "/api/v1/4" => ApiExampleController.Index(4)
    // GET "/api/v1/4/detail => ApiExampleController.Detail(4)

    Console.WriteLine("Serving files, press enter to stop ...");
    Console.ReadLine();

    tcs.Cancel(); // Request stop
    task.Wait();  // Wait for shutdown
}
```

Running from the commandline
============================

Instead of hosting the server from an executable, it is possible to use the commandline interface to manage the sockets.

Write the routes as described above, for example:
```csharp
[Name("api")]
public interface IAPI : IControllerPrefix { }

[Name("v1")]
public interface IApiV1 : IAPI { }

[Name("entry")]
public class ApiExampleController : Controller, IApiV1
{
    [HttpGet]
    public IResult Index(IHttpContext context)
    {
        return OK();
    }

    public IResult Index(int id)
    {
        return Html("<body>Hello!</body>");
    }

    [Route("{id}/detail")]
    public IResult Detail(int id)
    {
        return Status(HttpStatusCode.NoContent);
    }
}
```

Then provide a configuration file ([see example-config file for more options](Ceen.Httpd.Cli/example_config.txt)):
```
httpport 80
httpaddress any

# send combined-log-format data to stdout
logger Ceen.Httpd Ceen.Httpd.Logging.CLFStdOut

# load the mvc based assembly, set the ApiExampleController as default route
route MyAssembly MyAssembly.MyNamespace.ApiExampleController

# serve files from a local folder
serve "" "/usr/share/www"
```

With the route compiled into `MyAssembly.dll` and placed in the same folder as `Ceen.Httpd.Cli.exe` run:
```
mono Ceen.Httpd.Cli.exe config.txt
```

This will load the configuration file and set up the server like configured.
To update the application, replace `MyAssembly.dll` and send `SIGHUP` to the process, which will reload the config file and all assemblies.
Once the new assembly is loaded, it will unload the old assembly, such that no client requests are lost.
