module Main exposing (Feedback(..), Guess, Model, Msg(..), enteredGuesses, hardModeError, initialModel, main, update, validationError)

import Browser
import Char
import Html exposing (Html, button, div, h1, input, label, p, section, span, text, textarea)
import Html.Attributes exposing (attribute, checked, class, classList, disabled, maxlength, placeholder, title, type_, value)
import Html.Events exposing (onCheck, onClick, onInput)
import Html.Keyed as Keyed
import Http
import Json.Decode as Decode
import Json.Encode as Encode
import String


type Feedback
    = Absent
    | Present
    | Correct


type alias Guess =
    { id : Int
    , guess : String
    , feedback : List (Maybe Feedback)
    }


type alias Model =
    { guesses : List Guess
    , possibilities : List String
    , count : Int
    , customCandidates : String
    , error : String
    , loading : Bool
    , hardMode : Bool
    , nextId : Int
    }


type Msg
    = GuessChanged Int String
    | FeedbackChanged Int Int
    | AddGuess
    | RemoveGuess Int
    | CustomCandidatesChanged String
    | HardModeChanged Bool
    | Solve
    | Solved Int (List String)
    | Failed String
    | Reset


main : Program () Model Msg
main =
    Browser.element
        { init = \_ -> ( initialModel, Cmd.none )
        , update = update
        , view = view
        , subscriptions = \_ -> Sub.none
        }


initialGuess : Int -> Guess
initialGuess id =
    { id = id
    , guess = ""
    , feedback = List.repeat 5 Nothing
    }


initialModel : Model
initialModel =
    { guesses = [ initialGuess 1 ]
    , possibilities = []
    , count = 0
    , customCandidates = ""
    , error = ""
    , loading = False
    , hardMode = False
    , nextId = 2
    }


cleanGuess : String -> String
cleanGuess value =
    value
        |> String.left 5
        |> String.filter Char.isAlpha
        |> String.toLower


parseCandidates : String -> List String
parseCandidates value =
    value
        |> String.split ","
        |> List.concatMap String.words
        |> List.map (String.trim >> String.toLower)
        |> List.filter (not << String.isEmpty)


enteredGuesses : Model -> List Guess
enteredGuesses model =
    model.guesses
        |> List.filter (\guess -> not (String.isEmpty guess.guess))


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        GuessChanged id newGuess ->
            ( { model | guesses = updateGuess id (\guess -> { guess | guess = cleanGuess newGuess }) model.guesses }
            , Cmd.none
            )

        FeedbackChanged id index ->
            ( { model | guesses = updateGuess id (\guess -> { guess | feedback = updateFeedbackAt index nextFeedback guess.feedback }) model.guesses }
            , Cmd.none
            )

        AddGuess ->
            if canAddGuess model then
                ( { model
                    | guesses = model.guesses ++ [ initialGuess model.nextId ]
                    , nextId = model.nextId + 1
                  }
                , Cmd.none
                )

            else
                ( model, Cmd.none )

        RemoveGuess id ->
            ( { model | guesses = List.filter (\guess -> guess.id /= id) model.guesses }
            , Cmd.none
            )

        CustomCandidatesChanged value ->
            ( { model | customCandidates = value }, Cmd.none )

        HardModeChanged value ->
            ( { model | hardMode = value }, Cmd.none )

        Solve ->
            let
                currentValidationError =
                    validationError model
            in
            if not (String.isEmpty currentValidationError) then
                ( { model | loading = False, error = currentValidationError }
                , Cmd.none
                )

            else if List.isEmpty (enteredGuesses model) then
                ( { model | loading = False, error = "Enter at least one guess with feedback." }
                , Cmd.none
                )

            else
                ( { model | loading = True, error = "" }
                , solve model
                )

        Solved count possibilities ->
            ( { model
                | loading = False
                , error = ""
                , count = count
                , possibilities = possibilities
              }
            , Cmd.none
            )

        Failed error ->
            ( { model | loading = False, error = error }, Cmd.none )

        Reset ->
            ( initialModel, Cmd.none )


updateGuess : Int -> (Guess -> Guess) -> List Guess -> List Guess
updateGuess id transform guesses =
    List.map
        (\guess ->
            if guess.id == id then
                transform guess

            else
                guess
        )
        guesses


updateFeedbackAt : Int -> (Maybe Feedback -> Maybe Feedback) -> List (Maybe Feedback) -> List (Maybe Feedback)
updateFeedbackAt index transform feedback =
    List.indexedMap
        (\itemIndex item ->
            if itemIndex == index then
                transform item

            else
                item
        )
        feedback


nextFeedback : Maybe Feedback -> Maybe Feedback
nextFeedback feedback =
    case feedback of
        Nothing ->
            Just Absent

        Just Absent ->
            Just Present

        Just Present ->
            Just Correct

        Just Correct ->
            Just Absent


canAddGuess : Model -> Bool
canAddGuess model =
    List.all guessComplete model.guesses
        && String.isEmpty (hardModeError model)


validationError : Model -> String
validationError model =
    case guessInputError model of
        Just error ->
            error

        Nothing ->
            hardModeError model


guessComplete : Guess -> Bool
guessComplete guess =
    String.length guess.guess == 5 && feedbackComplete guess


guessInputError : Model -> Maybe String
guessInputError model =
    let
        entered =
            enteredGuesses model
    in
    if List.any (\guess -> String.length guess.guess /= 5) entered then
        Just "Enter five letters for each guess."

    else if List.any (not << feedbackComplete) entered then
        Just "Select feedback for each entered guess."

    else
        Nothing


feedbackComplete : Guess -> Bool
feedbackComplete guess =
    List.all
        (\feedback ->
            case feedback of
                Just _ ->
                    True

                Nothing ->
                    False
        )
        guess.feedback


hardModeError : Model -> String
hardModeError model =
    if model.hardMode then
        model.guesses
            |> List.filter (\guess -> String.length guess.guess == 5 && feedbackComplete guess)
            |> hardModeViolation
            |> Maybe.withDefault ""

    else
        ""


hardModeViolation : List Guess -> Maybe String
hardModeViolation guesses =
    let
        validate previous remaining =
            case remaining of
                [] ->
                    Nothing

                current :: rest ->
                    case guessViolation previous current of
                        Just violation ->
                            Just violation

                        Nothing ->
                            validate (previous ++ [ current ]) rest
    in
    case guesses of
        [] ->
            Nothing

        [ _ ] ->
            Nothing

        first :: rest ->
            validate [ first ] rest


guessViolation : List Guess -> Guess -> Maybe String
guessViolation previousGuesses currentGuess =
    let
        greenPositions =
            previousGuesses
                |> List.concatMap greenLetters

        requiredCounts =
            previousGuesses
                |> List.map (positiveLetters >> requiredLetterCounts)
                |> List.foldl mergeRequiredCounts []

        missingGreen =
            greenPositions
                |> List.filterMap
                    (\( index, letter ) ->
                        if charAt index currentGuess.guess == Just letter then
                            Nothing

                        else
                            Just ( index, letter )
                    )
                |> List.head
    in
    case missingGreen of
        Just ( index, letter ) ->
            Just ("Guess '" ++ currentGuess.guess ++ "' must use '" ++ String.fromChar letter ++ "' in position " ++ String.fromInt (index + 1) ++ ".")

        Nothing ->
            requiredCounts
                |> List.filter (\( letter, count ) -> letterCount letter currentGuess.guess < count)
                |> List.head
                |> Maybe.map
                    (\( letter, count ) ->
                        if count == 1 then
                            "Guess '" ++ currentGuess.guess ++ "' must include '" ++ String.fromChar letter ++ "'."

                        else
                            "Guess '" ++ currentGuess.guess ++ "' must include " ++ String.fromInt count ++ " copies of '" ++ String.fromChar letter ++ "'."
                    )


greenLetters : Guess -> List ( Int, Char )
greenLetters guess =
    List.map2 Tuple.pair (String.toList guess.guess) guess.feedback
        |> List.indexedMap Tuple.pair
        |> List.filterMap
            (\( index, ( letter, feedback ) ) ->
                case feedback of
                    Just Correct ->
                        Just ( index, letter )

                    _ ->
                        Nothing
            )


positiveLetters : Guess -> List Char
positiveLetters guess =
    List.map2 Tuple.pair (String.toList guess.guess) guess.feedback
        |> List.filterMap
            (\( letter, feedback ) ->
                case feedback of
                    Just Correct ->
                        Just letter

                    Just Present ->
                        Just letter

                    Just Absent ->
                        Nothing

                    Nothing ->
                        Nothing
            )


requiredLetterCounts : List Char -> List ( Char, Int )
requiredLetterCounts letters =
    letters
        |> List.foldl
            (\letter counts ->
                incrementLetterCount letter counts
            )
            []


mergeRequiredCounts : List ( Char, Int ) -> List ( Char, Int ) -> List ( Char, Int )
mergeRequiredCounts next current =
    next
        |> List.foldl
            (\( letter, count ) counts ->
                mergeRequiredCount letter count counts
            )
            current


mergeRequiredCount : Char -> Int -> List ( Char, Int ) -> List ( Char, Int )
mergeRequiredCount letter count counts =
    case counts of
        [] ->
            [ ( letter, count ) ]

        ( currentLetter, currentCount ) :: rest ->
            if currentLetter == letter then
                ( currentLetter, max currentCount count ) :: rest

            else
                ( currentLetter, currentCount ) :: mergeRequiredCount letter count rest


incrementLetterCount : Char -> List ( Char, Int ) -> List ( Char, Int )
incrementLetterCount letter counts =
    case counts of
        [] ->
            [ ( letter, 1 ) ]

        ( currentLetter, count ) :: rest ->
            if currentLetter == letter then
                ( currentLetter, count + 1 ) :: rest

            else
                ( currentLetter, count ) :: incrementLetterCount letter rest


letterCount : Char -> String -> Int
letterCount letter word =
    word
        |> String.toList
        |> List.filter ((==) letter)
        |> List.length


charAt : Int -> String -> Maybe Char
charAt index word =
    word
        |> String.toList
        |> List.drop index
        |> List.head


feedbackLabel : Feedback -> String
feedbackLabel feedback =
    case feedback of
        Absent ->
            "B"

        Present ->
            "Y"

        Correct ->
            "G"


feedbackValueLabel : Maybe Feedback -> String
feedbackValueLabel feedback =
    feedback
        |> Maybe.map feedbackLabel
        |> Maybe.withDefault ""


feedbackClass : Maybe Feedback -> String
feedbackClass feedback =
    case feedback of
        Nothing ->
            "unset"

        Just Absent ->
            "absent"

        Just Present ->
            "present"

        Just Correct ->
            "correct"


solve : Model -> Cmd Msg
solve model =
    Http.post
        { url = "/api/solve"
        , body = Http.jsonBody (solveRequestEncoder model)
        , expect = Http.expectStringResponse responseToMsg decodeSolveResponse
        }


responseToMsg : Result String ( Int, List String ) -> Msg
responseToMsg result =
    case result of
        Ok ( count, possibilities ) ->
            Solved count possibilities

        Err error ->
            Failed error


decodeSolveResponse : Http.Response String -> Result String ( Int, List String )
decodeSolveResponse response =
    case response of
        Http.GoodStatus_ _ body ->
            Decode.decodeString solveResponseDecoder body
                |> Result.mapError (\_ -> "The solver API returned an unexpected response.")

        Http.BadStatus_ _ body ->
            Decode.decodeString errorResponseDecoder body
                |> Result.withDefault "Unable to solve right now."
                |> Err

        Http.BadUrl_ _ ->
            Err "The solver API is not reachable."

        Http.Timeout_ ->
            Err "The solver API is not reachable."

        Http.NetworkError_ ->
            Err "The solver API is not reachable."


solveRequestEncoder : Model -> Encode.Value
solveRequestEncoder model =
    let
        candidates =
            parseCandidates model.customCandidates
    in
    Encode.object
        [ ( "guesses", Encode.list guessEncoder (enteredGuesses model) )
        , ( "hardMode", Encode.bool model.hardMode )
        , ( "candidates"
          , if List.isEmpty candidates then
                Encode.null

            else
                Encode.list Encode.string candidates
          )
        ]


guessEncoder : Guess -> Encode.Value
guessEncoder guess =
    Encode.object
        [ ( "guess", Encode.string guess.guess )
        , ( "feedback", Encode.string (String.concat (List.map feedbackLabel (List.filterMap identity guess.feedback))) )
        ]


solveResponseDecoder : Decode.Decoder ( Int, List String )
solveResponseDecoder =
    Decode.map2 Tuple.pair
        (Decode.field "count" Decode.int)
        (Decode.field "possibilities" (Decode.list Decode.string))


errorResponseDecoder : Decode.Decoder String
errorResponseDecoder =
    Decode.field "error" Decode.string


view : Model -> Html Msg
view model =
    let
        currentValidationError =
            validationError model

        addGuessDisabled =
            not (canAddGuess model)

        visibleError =
            if String.isEmpty currentValidationError then
                model.error

            else
                currentValidationError
    in
    div [ class "shell", attribute "role" "main" ]
        [ section [ class "panel" ]
            [ h1 [] [ text "Wordle Solver" ]
            , p [ class "lede" ] [ text "Enter each guess and click feedback tiles until they match Wordle: G green, Y yellow, B gray." ]
            , Keyed.node "div"
                [ class "guess-list" ]
                (List.map (\guess -> ( String.fromInt guess.id, guessView model guess )) model.guesses)
            , div [ class "actions" ]
                [ button [ class "primary", disabled (not (String.isEmpty currentValidationError)), onClick Solve ] [ text (if model.loading then "Solving..." else "Solve") ]
                , button [ class "secondary", disabled addGuessDisabled, onClick AddGuess ] [ text "Add Guess" ]
                , button [ class "secondary", onClick Reset ] [ text "Reset" ]
                ]
            , p [ class "error", attribute "role" "status" ] [ text visibleError ]
            , div [ class "solve-options" ]
                [ label [ class "hard-mode" ]
                    [ input
                        [ type_ "checkbox"
                        , checked model.hardMode
                        , onCheck HardModeChanged
                        ]
                        []
                    , span [] [ text "Hard Mode" ]
                    ]
                , div [ class "candidate-input" ]
                    [ label [ class "hint", attribute "for" "custom-candidates" ] [ text "Optional candidate words" ]
                    , textarea
                        [ value model.customCandidates
                        , attribute "id" "custom-candidates"
                        , attribute "data-focus-key" "custom-candidates"
                        , placeholder "Paste five-letter answers separated by spaces, commas, or new lines."
                        , onInput CustomCandidatesChanged
                        ]
                        []
                    ]
                ]
            ]
        , section [ class "results" ]
            [ div [ class "summary" ]
                [ div []
                    [ div [ class "count" ] [ text (String.fromInt model.count) ]
                    , div [ class "hint" ] [ text "possible answers" ]
                    ]
                ]
            , div [ class "word-grid" ] (List.map wordView model.possibilities)
            ]
        ]


guessView : Model -> Guess -> Html Msg
guessView model guess =
    section [ class "guess-row" ]
        [ input
            [ class "word-input"
            , value guess.guess
            , maxlength 5
            , placeholder "GUESS"
            , attribute "data-focus-key" ("guess-" ++ String.fromInt guess.id)
            , attribute "aria-label" "Guess"
            , onInput (GuessChanged guess.id)
            ]
            []
        , div [ class "feedback-grid" ]
            (List.indexedMap (feedbackButton guess.id) guess.feedback)
        , if List.length model.guesses > 1 then
            button [ class "remove", onClick (RemoveGuess guess.id) ] [ text "Remove" ]

          else
            span [] []
        ]


feedbackButton : Int -> Int -> Maybe Feedback -> Html Msg
feedbackButton id index feedback =
    button
        [ classList [ ( "tile", True ), ( feedbackClass feedback, True ) ]
        , title "Cycle feedback"
        , attribute "aria-label" ("Feedback position " ++ String.fromInt (index + 1))
        , onClick (FeedbackChanged id index)
        ]
        [ text (feedbackValueLabel feedback) ]


wordView : String -> Html Msg
wordView word =
    div [ class "word" ] [ text word ]
