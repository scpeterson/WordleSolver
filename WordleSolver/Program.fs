open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open WordleSolver.Domain

type GuessRequest =
    { guess: string
      feedback: string }

type SolveRequest =
    { guesses: GuessRequest array
      candidates: string array option }

type SolveResponse =
    { count: int
      possibilities: string array }

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

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore

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
    |> ignore

    app.Run()

    0 // Exit code
