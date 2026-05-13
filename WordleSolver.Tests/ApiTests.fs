module WordleSolver.ApiTests

open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Net.Http
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Xunit

let private findWebProjectRoot () =
    let rec search directory =
        let candidate = Path.Combine(directory, "WordleSolver", "wwwroot", "index.html")

        if File.Exists candidate then
            Path.Combine(directory, "WordleSolver")
        else
            let parent = Directory.GetParent directory

            if isNull parent then
                failwith "Could not locate WordleSolver/wwwroot/index.html"
            else
                search parent.FullName

    search (Directory.GetCurrentDirectory())

let private createClient permitLimit =
    let builder =
        WebApplication.CreateBuilder(
            WebApplicationOptions(
                Args = [||],
                ContentRootPath = findWebProjectRoot()))

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
        """{"guesses":[{"guess":"crane","feedback":"BBBBB"}],"candidates":["sloth"],"hardMode":false}""",
        Encoding.UTF8,
        "application/json")

let private hardModeViolationRequest () =
    new StringContent(
        """{"guesses":[{"guess":"crane","feedback":"GBBBB"},{"guess":"sloth","feedback":"BBBBB"}],"candidates":["sloth"],"hardMode":true}""",
        Encoding.UTF8,
        "application/json")

let private postSolve (client: HttpClient) =
    client.PostAsync("/api/solve", solveRequest()).GetAwaiter().GetResult()

let private get (client: HttpClient) (path: string) =
    client.GetAsync(path).GetAwaiter().GetResult()

let private readBody (response: HttpResponseMessage) =
    response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

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
let ``solve endpoint rejects hard mode violations`` () =
    let client, app = createClient 30

    try
        use response = client.PostAsync("/api/solve", hardModeViolationRequest()).GetAwaiter().GetResult()

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)

        let body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Assert.Contains("must use 'c' in position 1", body)
    finally
        app.DisposeAsync().AsTask().GetAwaiter().GetResult()

[<Fact>]
let ``static frontend assets are served from wwwroot`` () =
    let client, app = createClient 30

    try
        use index = get client "/"
        use script = get client "/app.js"
        use styles = get client "/styles.css"

        Assert.Equal(HttpStatusCode.OK, index.StatusCode)
        Assert.Equal(HttpStatusCode.OK, script.StatusCode)
        Assert.Equal(HttpStatusCode.OK, styles.StatusCode)

        Assert.Equal("text/html", index.Content.Headers.ContentType.MediaType)
        Assert.Equal("text/javascript", script.Content.Headers.ContentType.MediaType)
        Assert.Equal("text/css", styles.Content.Headers.ContentType.MediaType)

        Assert.Contains("""<script src="/app.js"></script>""", readBody index)
        Assert.Contains("Elm.Main", readBody script)
        Assert.Contains("body", readBody styles)
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
