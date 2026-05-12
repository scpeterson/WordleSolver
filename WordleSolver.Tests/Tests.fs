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

let private expectError (expected: SolveError) result =
    match result with
    | Ok value -> failwith $"Expected %A{expected}, but got %A{value}."
    | Error actual -> Assert.Equal(expected, actual)

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
let ``parser rejects guesses that are not exactly five letters`` () =
    parseGuessFeedback "four" "BBBBB"
    |> expectError (InvalidGuessLength "four")

[<Fact>]
let ``parser rejects feedback that is not exactly five tokens`` () =
    parseGuessFeedback "trace" "BBBB"
    |> expectError (InvalidFeedbackLength "BBBB")

[<Fact>]
let ``parser rejects unrecognized feedback tokens`` () =
    parseGuessFeedback "trace" "BBZBB"
    |> expectError (InvalidFeedbackToken 'Z')

[<Fact>]
let ``parser accepts color and semantic feedback aliases`` () =
    let parsed = parse "trace" "CPAXG"

    Assert.Equal("trace", parsed.Guess)
    Assert.Equal<Feedback list>([ Correct; Present; Absent; Absent; Correct ], parsed.Feedback)

[<Fact>]
let ``solve normalizes filters deduplicates and sorts candidates`` () =
    let guesses = [ parse "trace" "GGGGG" ]
    let candidates = [ " trace "; "TRACE"; "traces"; "tr@ce"; "crane"; "Trace" ]

    let possibilities = solve candidates guesses

    Assert.Equal<string list>([ "trace" ], possibilities)

[<Fact>]
let ``hard mode requires green letters in subsequent guesses`` () =
    [ parse "crane" "GBBBB"; parse "sloth" "BBBBB" ]
    |> validateHardMode
    |> expectError (HardModeViolation("sloth", "Guess 'sloth' must use 'c' in position 1."))

[<Fact>]
let ``hard mode requires revealed yellow letters in subsequent guesses`` () =
    [ parse "crane" "YBBBB"; parse "sloth" "BBBBB" ]
    |> validateHardMode
    |> expectError (HardModeViolation("sloth", "Guess 'sloth' must include 'c'."))

[<Fact>]
let ``hard mode allows subsequent guesses that use revealed hints`` () =
    let result =
        [ parse "crane" "GYBBB"; parse "cider" "BBBBB" ]
        |> validateHardMode

    match result with
    | Ok guesses -> Assert.Equal(2, guesses.Length)
    | Error error -> failwith (explainError error)

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
