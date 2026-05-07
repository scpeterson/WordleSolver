#load "../WordleSolver/Domain.fs"

open WordleSolver.Domain

let private tokens feedback =
    feedback
    |> List.map (function
        | Correct -> "G"
        | Present -> "Y"
        | Absent -> "B")
    |> String.concat ""

let private expect name expected actual =
    if actual <> expected then
        failwith $"{name}: expected {expected}, got {actual}"

let private score answer guess =
    match scoreGuess answer guess with
    | Ok feedback -> tokens feedback
    | Error error -> failwith (explainError error)

let private parse guess feedback =
    match parseGuessFeedback guess feedback with
    | Ok parsed -> parsed
    | Error error -> failwith (explainError error)

expect "trace against budge" "BBBBG" (score "budge" "trace")
expect "spine against budge" "BBBBG" (score "budge" "spine")
expect "whole against budge" "BBBBG" (score "budge" "whole")
expect "judge against budge" "BGGGG" (score "budge" "judge")
expect "eerie exact" "GGGGG" (score "eerie" "eerie")
expect "extra e copies are absent after green e is consumed" "BBBBG" (score "budge" "eerie")
expect "three e answer with two green e tiles" "GBBBG" (score "eerie" "eagle")

let candidates = [ "budge"; "judge"; "eerie"; "spine"; "trace"; "whole" ]

let firstPass =
    solve
        candidates
        [ parse "trace" "BBBBG"
          parse "spine" "BBBBG"
          parse "whole" "BBBBG" ]

expect "budge remains possible" true (firstPass |> List.contains "budge")
expect "judge also remains possible before another guess" true (firstPass |> List.contains "judge")

let secondPass =
    solve
        candidates
        [ parse "trace" "BBBBG"
          parse "spine" "BBBBG"
          parse "whole" "BBBBG"
          parse "judge" "BGGGG" ]

expect "judge feedback narrows to budge" [ "budge" ] secondPass

printfn "Solver checks passed."
