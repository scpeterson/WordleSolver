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
        | NoGuesses -> "Enter at least one guess with feedback."
