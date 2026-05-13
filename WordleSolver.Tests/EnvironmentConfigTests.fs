module WordleSolver.EnvironmentConfigTests

open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Xunit

let private findWebProjectRoot () =
    let rec search directory =
        let candidate = Path.Combine(directory, "WordleSolver", "appsettings.json")

        if File.Exists candidate then
            Path.Combine(directory, "WordleSolver")
        else
            let parent = Directory.GetParent directory

            if isNull parent then
                failwith "Could not locate WordleSolver/appsettings.json"
            else
                search parent.FullName

    search (Directory.GetCurrentDirectory())

let private loadConfiguration environment =
    ConfigurationBuilder()
        .SetBasePath(findWebProjectRoot())
        .AddJsonFile("appsettings.json", optional = false)
        .AddJsonFile($"appsettings.{environment}.json", optional = false)
        .Build()

[<Theory>]
[<InlineData("DEV")>]
[<InlineData("QA")>]
[<InlineData("STAGE")>]
[<InlineData("PROD")>]
let ``environment appsettings files load required solver configuration`` environment =
    let configuration = loadConfiguration environment

    Assert.False(System.String.IsNullOrWhiteSpace(configuration["Logging:LogLevel:Default"]))
    Assert.Equal("30", configuration["RateLimiting:Solve:PermitLimit"])
    Assert.Equal("60", configuration["RateLimiting:Solve:WindowSeconds"])

[<Theory>]
[<InlineData("DEV")>]
[<InlineData("QA")>]
[<InlineData("STAGE")>]
[<InlineData("PROD")>]
let ``app starts with named environment configuration`` environment =
    let builder =
        WebApplication.CreateBuilder(
            WebApplicationOptions(
                Args = [||],
                ContentRootPath = findWebProjectRoot(),
                EnvironmentName = environment))

    builder.WebHost.UseTestServer() |> ignore

    Program.configureServices builder
    let app = builder.Build()
    Program.configureApp app

    try
        app.StartAsync().GetAwaiter().GetResult()
    finally
        app.DisposeAsync().AsTask().GetAwaiter().GetResult()
