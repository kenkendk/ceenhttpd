Ceenhttpd
=========

A tiny and efficient CIL web server. 

Ceedhttpd is meant for use with high-load webservers where mod_mono or another CGI-like approach would normally be required. By hosting the entire webserver in CIL, the overhead associcated with CGI, FCGI and SCGI is removed. If the handling code relies on `async`/`await` as the server does, it can scale to a very high number of simultaneous connections.

Ceedhttpd is also very useful for devices with limited processing power and Mono. On a device with limited resources, it would be infeasible to run a full webserver, but running a small server like Ceenhttpd makes it a viable approach.

Key features:

  - Small codebase - easy code overview
  - SSL support - secure connections
  - Async implementation - handles high number of concurrent requests
  - No dependencies - embed where you need it
  - Built on TCP level - easy to debug
  - Basic modules included - W3C Log and static file serving
  - REST aware - Implement custom REST logic

Missing features:

  - No MVC or template engine: use [T4](https://msdn.microsoft.com/en-us/library/bb126445.aspx) or your favorite
  - No standalone server: embed and configure from your own code
  - No logging support: [Use log4net](https://logging.apache.org/log4net/)

Installation
============

[Ceenhttpd is available on NuGet](https://www.nuget.org/packages/Ceenhttpd/):
```
PM Install-Package Ceenhttpd
```

Example
=======

Running a webserver for static content is easy:

```csharp
using System;
using Ceenhttpd;
using Ceenhttpd.Handler;
using System.Net;
using System.Threading;

...

public static void Main(string[] args)
{
    var server = new HttpServer(new ServerConfig() {
        Router = new Router(
            new Tuple<string, IHttpModule>[] {
                new Tuple<string, IHttpModule>(
                    "[.*]", 
                    new FileHandler(args.Length == 0 ? "." : args[0])
                )
            }
        ),

        Logger = new CLFLogger(Console.OpenStandardOutput())
    });

    var tcs = new CancellationTokenSource();
    var task = server.Listen(new IPEndPoint(IPAddress.Any, 8080), tcs);

    Console.WriteLine("Serving files, press enter to stop ...");
    Console.ReadLine();

    tcs.Cancel(); // Request stop
    task.Wait();  // Wait for shutdown
}
```


Dynamic content can be added with a simple handler:

```csharp
using System;
using Ceenhttpd;
using Ceenhttpd.Handler;
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
    var server = new HttpServer(new ServerConfig() {
        Router = new Router(
            new Tuple<string, IHttpModule>[] {
                new Tuple<string, IHttpModule>(
                    "/timeofday", 
                    new TimeOfDayHandler()
                ),

                new Tuple<string, IHttpModule>(
                    "[.*]", 
                    new FileHandler(args.Length == 0 ? "." : args[0])
                )
            }
        ),

        Logger = new CLFLogger(Console.OpenStandardOutput())
    });

    var tcs = new CancellationTokenSource();
    var task = server.Listen(new IPEndPoint(IPAddress.Any, 8080), tcs);

    Console.WriteLine("Serving files, press enter to stop ...");
    Console.ReadLine();

    tcs.Cancel(); // Request stop
    task.Wait();  // Wait for shutdown
}
```