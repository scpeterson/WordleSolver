namespace WordleSolver

open System

module Domain =
    [<Literal>]
    let WordLength = 5

    type Feedback =
        | Correct
        | Present
        | Absent

    type GuessFeedback =
        { Guess: string
          Feedback: Feedback list }

    type SolveError =
        | InvalidGuessLength of string
        | InvalidFeedbackLength of string
        | InvalidFeedbackToken of char
        | HardModeViolation of guess: string * requirement: string
        | NoGuesses

    let private normalizeWord (word: string) =
        word.Trim().ToLowerInvariant()

    let isFiveLetterWord (word: string) =
        let normalized = normalizeWord word

        normalized.Length = WordLength
        && normalized |> Seq.forall Char.IsAsciiLetterLower

    let parseFeedbackToken token =
        match Char.ToUpperInvariant token with
        | 'G'
        | 'C' -> Ok Correct
        | 'Y'
        | 'P' -> Ok Present
        | 'B'
        | 'A'
        | 'X' -> Ok Absent
        | other -> Error(InvalidFeedbackToken other)

    let parseGuessFeedback (guess: string) (feedback: string) =
        let normalizedGuess = normalizeWord guess
        let normalizedFeedback = feedback.Trim()

        if normalizedGuess.Length <> WordLength then
            Error(InvalidGuessLength guess)
        elif normalizedFeedback.Length <> WordLength then
            Error(InvalidFeedbackLength feedback)
        else
            normalizedFeedback
            |> Seq.map parseFeedbackToken
            |> Seq.fold
                (fun state tokenResult ->
                    match state, tokenResult with
                    | Error error, _ -> Error error
                    | _, Error error -> Error error
                    | Ok tokens, Ok token -> Ok(token :: tokens))
                (Ok [])
            |> Result.map (fun tokens ->
                { Guess = normalizedGuess
                  Feedback = List.rev tokens })

    let scoreGuess (answer: string) (guess: string) =
        let answerChars = normalizeWord answer |> Seq.toArray
        let guessChars = normalizeWord guess |> Seq.toArray

        if answerChars.Length <> WordLength then
            Error(InvalidGuessLength answer)
        elif guessChars.Length <> WordLength then
            Error(InvalidGuessLength guess)
        else
            let exactMatches =
                Array.map2 (=) answerChars guessChars

            let available =
                answerChars
                |> Array.mapi (fun index letter -> index, letter)
                |> Array.filter (fun (index, _) -> not exactMatches[index])
                |> Array.countBy snd
                |> Map.ofArray

            let useAvailable letter remaining =
                match Map.tryFind letter remaining with
                | Some count when count > 1 -> true, Map.add letter (count - 1) remaining
                | Some 1 -> true, Map.remove letter remaining
                | _ -> false, remaining

            let feedback, _ =
                (([], available), [ 0 .. WordLength - 1 ])
                ||> List.fold (fun (tokens, remaining) index ->
                    if exactMatches[index] then
                        (Correct :: tokens, remaining)
                    else
                        let isPresent, nextRemaining = useAvailable guessChars[index] remaining
                        ((if isPresent then Present else Absent) :: tokens, nextRemaining))

            Ok(List.rev feedback)

    let private matchesGuess candidate guessFeedback =
        scoreGuess candidate guessFeedback.Guess
        |> Result.map ((=) guessFeedback.Feedback)
        |> Result.defaultValue false

    let private positiveLetterCounts guessFeedback =
        guessFeedback.Feedback
        |> List.zip (guessFeedback.Guess |> Seq.toList)
        |> List.choose (function
            | letter, Correct
            | letter, Present -> Some letter
            | _, Absent -> None)
        |> List.countBy id

    let private mergeRequiredCounts current next =
        next
        |> List.fold
            (fun required (letter, count) ->
                let existing = required |> Map.tryFind letter |> Option.defaultValue 0
                required |> Map.add letter (max existing count))
            current

    let private hardModeRequirements previousGuesses =
        let greenPositions =
            previousGuesses
            |> List.collect (fun guessFeedback ->
                guessFeedback.Feedback
                |> List.mapi (fun index feedback -> index, feedback)
                |> List.choose (function
                    | index, Correct -> Some(index, guessFeedback.Guess[index])
                    | _ -> None))

        let requiredCounts =
            previousGuesses
            |> List.map positiveLetterCounts
            |> List.fold mergeRequiredCounts Map.empty

        greenPositions, requiredCounts

    let private letterCount letter guess =
        guess
        |> Seq.filter ((=) letter)
        |> Seq.length

    let private hardModeViolation previousGuesses currentGuess =
        let greenPositions, requiredCounts = hardModeRequirements previousGuesses

        let missingGreen =
            greenPositions
            |> List.tryFind (fun (index, letter) -> currentGuess.Guess[index] <> letter)

        match missingGreen with
        | Some(index, letter) -> Some $"Guess '{currentGuess.Guess}' must use '{letter}' in position {index + 1}."
        | None ->
            requiredCounts
            |> Map.toList
            |> List.tryFind (fun (letter, count) -> letterCount letter currentGuess.Guess < count)
            |> Option.map (fun (letter, count) ->
                if count = 1 then
                    $"Guess '{currentGuess.Guess}' must include '{letter}'."
                else
                    $"Guess '{currentGuess.Guess}' must include {count} copies of '{letter}'.")

    let validateHardMode guesses =
        let rec validate previous remaining =
            match remaining with
            | [] -> Ok guesses
            | current :: rest ->
                match hardModeViolation previous current with
                | Some violation -> Error(HardModeViolation(current.Guess, violation))
                | None -> validate (previous @ [ current ]) rest

        match guesses with
        | []
        | [ _ ] -> Ok guesses
        | first :: rest -> validate [ first ] rest

    let solve candidates guesses =
        guesses
        |> List.fold
            (fun remaining guess ->
                remaining
                |> List.filter (fun candidate -> matchesGuess candidate guess))
            (candidates |> List.map normalizeWord |> List.filter isFiveLetterWord |> List.distinct |> List.sort)

    let explainError =
        function
        | InvalidGuessLength value -> $"'{value}' must be exactly five letters."
        | InvalidFeedbackLength value -> $"Feedback '{value}' must contain exactly five tokens."
        | InvalidFeedbackToken value -> $"Feedback token '{value}' is not recognized. Use G/Y/B, C/P/A, or X for absent."
        | HardModeViolation(_, requirement) -> requirement
        | NoGuesses -> "Enter at least one guess with feedback."
