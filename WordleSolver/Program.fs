open System
open System.IO
open System.Threading.RateLimiting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.RateLimiting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open WordleSolver.Domain

type GuessRequest =
    { guess: string
      feedback: string }

type SolveRequest =
    { guesses: GuessRequest array
      candidates: string array option
      hardMode: bool }

type SolveResponse =
    { count: int
      possibilities: string array }

type RateLimitOptions =
    { PermitLimit: int
      WindowSeconds: int }

let private defaultRateLimitOptions =
    { PermitLimit = 30
      WindowSeconds = 60 }

let private answerListPath =
    Path.Combine(AppContext.BaseDirectory, "Data", "answers.txt")

let private defaultCandidates =
    if File.Exists answerListPath then
        File.ReadAllLines answerListPath |> Array.toList
    else
        []

let private parseRequest (request: SolveRequest) =
    let guesses =
        request.guesses
        |> Array.toList
        |> List.filter (fun item -> not (String.IsNullOrWhiteSpace item.guess) || not (String.IsNullOrWhiteSpace item.feedback))

    match guesses with
    | [] -> Error NoGuesses
    | items ->
        items
        |> List.map (fun item -> parseGuessFeedback item.guess item.feedback)
        |> List.fold
            (fun state item ->
                match state, item with
                | Error error, _ -> Error error
                | _, Error error -> Error error
                | Ok parsed, Ok guess -> Ok(guess :: parsed))
            (Ok [])
        |> Result.map List.rev
        |> Result.bind (fun guesses ->
            if request.hardMode then
                validateHardMode guesses
            else
                Ok guesses)

let configureServices (builder: WebApplicationBuilder) =
    let rateLimit =
        builder.Configuration.GetSection("RateLimiting:Solve").Get<RateLimitOptions>()
        |> Option.ofObj
        |> Option.defaultValue defaultRateLimitOptions

    builder.Services.AddRateLimiter(fun options ->
        options.RejectionStatusCode <- StatusCodes.Status429TooManyRequests

        options.OnRejected <-
            Func<OnRejectedContext, Threading.CancellationToken, Threading.Tasks.ValueTask>(fun context cancellationToken ->
                context.HttpContext.Response.ContentType <- "application/json"
                context.HttpContext.Response.WriteAsJsonAsync({| error = "Too many solve requests. Please wait a moment and try again." |}, cancellationToken)
                |> Threading.Tasks.ValueTask)

        options.AddPolicy("solve", fun context ->
            let partitionKey =
                match context.Connection.RemoteIpAddress with
                | null -> "unknown"
                | address -> address.ToString()

            RateLimitPartition.GetFixedWindowLimiter(partitionKey, fun _ ->
                FixedWindowRateLimiterOptions(
                    PermitLimit = rateLimit.PermitLimit,
                    Window = TimeSpan.FromSeconds(float rateLimit.WindowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true)))
        |> ignore)
    |> ignore

let configureApp (app: WebApplication) =
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseRateLimiter() |> ignore

    app.MapGet("/api/answers", Func<SolveResponse>(fun () ->
        let possibilities = defaultCandidates |> List.filter isFiveLetterWord |> List.distinct |> List.sort |> List.toArray

        { count = possibilities.Length
          possibilities = possibilities }))
    |> ignore

    app.MapPost("/api/solve", Func<SolveRequest, IResult>(fun request ->
        match parseRequest request with
        | Error error -> Results.BadRequest {| error = explainError error |}
        | Ok guesses ->
            let candidates =
                request.candidates
                |> Option.map Array.toList
                |> Option.defaultValue defaultCandidates

            let possibilities = solve candidates guesses |> List.toArray

            Results.Ok
                { count = possibilities.Length
                  possibilities = possibilities }))
        .RequireRateLimiting("solve")
    |> ignore

let createApp (args: string array) =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder

    let app = builder.Build()
    configureApp app
    app

[<EntryPoint>]
let main args =
    let app = createApp args
    app.Run()

    0 // Exit code
