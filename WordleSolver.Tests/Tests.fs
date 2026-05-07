module WordleSolver.Tests

open System
open System.IO
open Xunit
open WordleSolver.Domain

let private tokenString feedback =
    feedback
    |> List.map (function
        | Correct -> "G"
        | Present -> "Y"
        | Absent -> "B")
    |> String.concat ""

let private score answer guess =
    match scoreGuess answer guess with
    | Ok feedback -> tokenString feedback
    | Error error -> failwith (explainError error)

let private parse guess feedback =
    match parseGuessFeedback guess feedback with
    | Ok parsed -> parsed
    | Error error -> failwith (explainError error)

let private findAnswerList () =
    let rec search directory =
        let candidate = Path.Combine(directory, "WordleSolver", "Data", "answers.txt")

        if File.Exists candidate then
            candidate
        else
            let parent = Directory.GetParent directory

            if isNull parent then
                failwith "Could not locate WordleSolver/Data/answers.txt"
            else
                search parent.FullName

    search (Directory.GetCurrentDirectory())

let private answerList () =
    findAnswerList()
    |> File.ReadAllLines
    |> Array.toList

[<Fact>]
let ``scores the budge guesses from the reported game`` () =
    Assert.Equal("BBBBG", score "budge" "trace")
    Assert.Equal("BBBBG", score "budge" "spine")
    Assert.Equal("BBBBG", score "budge" "whole")
    Assert.Equal("BGGGG", score "budge" "judge")

[<Fact>]
let ``yellow letters are excluded from the position where they were guessed`` () =
    let candidates = [ "spine"; "horse"; "smile"; "style" ]
    let guesses = [ parse "house" "BBBYG" ]

    let possibilities = solve candidates guesses

    Assert.Equal<string list>([ "smile"; "spine"; "style" ], possibilities)
    Assert.DoesNotContain("horse", possibilities)

[<Fact>]
let ``duplicate letters consume only the available unmatched copies`` () =
    Assert.Equal("GGGGG", score "eerie" "eerie")
    Assert.Equal("BBBBG", score "budge" "eerie")
    Assert.Equal("GBBBG", score "eerie" "eagle")

[<Fact>]
let ``reported budge game includes budge with the default answer list`` () =
    let guesses =
        [ parse "trace" "BBBBG"
          parse "spine" "BBBBG"
          parse "whole" "BBBBG" ]

    let possibilities = solve (answerList()) guesses

    Assert.Equal<string list>([ "budge"; "femme"; "fudge"; "fugue"; "judge"; "queue" ], possibilities)

[<Fact>]
let ``judge feedback narrows the reported game to budge or fudge`` () =
    let guesses =
        [ parse "trace" "BBBBG"
          parse "spine" "BBBBG"
          parse "whole" "BBBBG"
          parse "judge" "BGGGG" ]

    let possibilities = solve (answerList()) guesses

    Assert.Equal<string list>([ "budge"; "fudge" ], possibilities)

[<Fact>]
let ``default answer list contains known repeated-letter answers`` () =
    let answers = answerList() |> Set.ofList

    Assert.Contains("budge", answers)
    Assert.Contains("eerie", answers)
    Assert.Contains("judge", answers)
