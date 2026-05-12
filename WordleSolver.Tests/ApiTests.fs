module WordleSolver.ApiTests

open System
open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Xunit

let private createClient permitLimit =
    let builder = WebApplication.CreateBuilder([||])
    builder.WebHost.UseTestServer() |> ignore

    builder.Configuration.AddInMemoryCollection(
        Dictionary<string, string>(
            dict
                [ "RateLimiting:Solve:PermitLimit", string permitLimit
                  "RateLimiting:Solve:WindowSeconds", "60" ]))
    |> ignore

    Program.configureServices builder
    let app = builder.Build()
    Program.configureApp app
    app.StartAsync().GetAwaiter().GetResult()
    app.GetTestClient(), app

let private solveRequest () =
    new StringContent(
        """{"guesses":[{"guess":"crane","feedback":"BBBBB"}],"candidates":["sloth"]}""",
        Encoding.UTF8,
        "application/json")

let private postSolve (client: HttpClient) =
    client.PostAsync("/api/solve", solveRequest()).GetAwaiter().GetResult()

[<Fact>]
let ``solve endpoint returns too many requests after configured limit`` () =
    let client, app = createClient 2

    try
        use first = postSolve client
        use second = postSolve client
        use third = postSolve client

        Assert.Equal(HttpStatusCode.OK, first.StatusCode)
        Assert.Equal(HttpStatusCode.OK, second.StatusCode)
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode)

        let body = third.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Assert.Contains("Too many solve requests", body)
    finally
        app.DisposeAsync().AsTask().GetAwaiter().GetResult()

[<Fact>]
let ``answers endpoint is not rate limited by solve policy`` () =
    let client, app = createClient 1

    try
        use first = client.GetAsync("/api/answers").GetAwaiter().GetResult()
        use second = client.GetAsync("/api/answers").GetAwaiter().GetResult()
        use third = client.GetAsync("/api/answers").GetAwaiter().GetResult()

        Assert.Equal(HttpStatusCode.OK, first.StatusCode)
        Assert.Equal(HttpStatusCode.OK, second.StatusCode)
        Assert.Equal(HttpStatusCode.OK, third.StatusCode)
    finally
        app.DisposeAsync().AsTask().GetAwaiter().GetResult()
