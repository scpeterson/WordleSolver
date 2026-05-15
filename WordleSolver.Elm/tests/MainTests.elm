module MainTests exposing (all)

import Expect
import Main exposing (Feedback(..), Msg(..))
import Test exposing (Test, describe, test)


all : Test
all =
    describe "Wordle Solver Elm client"
        [ test "initial blank guess is not considered entered" <|
            \_ ->
                Main.initialModel
                    |> Main.enteredGuesses
                    |> Expect.equal []
        , test "Solve with no entered guesses shows validation without loading" <|
            \_ ->
                let
                    ( model, _ ) =
                        Main.update Solve Main.initialModel
                in
                { error = model.error
                , loading = model.loading
                , count = model.count
                , possibilities = model.possibilities
                }
                    |> Expect.equal
                        { error = "Enter at least one guess with feedback."
                        , loading = False
                        , count = 0
                        , possibilities = []
                        }
        , test "enteredGuesses filters blank rows while preserving filled rows" <|
            \_ ->
                let
                    baseModel =
                        initialModelWithGuess "crane"

                    model =
                        { baseModel
                            | guesses =
                                [ { id = 1, guess = "", feedback = List.map Just [ Absent, Absent, Absent, Absent, Absent ] }
                                , { id = 2, guess = "crane", feedback = List.map Just [ Correct, Present, Absent, Absent, Absent ] }
                                ]
                        }
                in
                model
                    |> Main.enteredGuesses
                    |> List.map .guess
                    |> Expect.equal [ "crane" ]
        , test "HardModeChanged updates the solve request option state" <|
            \_ ->
                let
                    ( model, _ ) =
                        Main.update (HardModeChanged True) Main.initialModel
                in
                model.hardMode
                    |> Expect.equal True
        , test "Solve with a valid guess shows the loading state immediately" <|
            \_ ->
                let
                    ( model, _ ) =
                        initialModelWithGuess "crane"
                            |> Main.update Solve
                in
                { error = model.error
                , loading = model.loading
                }
                    |> Expect.equal
                        { error = ""
                        , loading = True
                        }
        , test "feedback starts unselected and first click selects absent feedback" <|
            \_ ->
                let
                    ( model, _ ) =
                        Main.update (FeedbackChanged 1 0) Main.initialModel
                in
                model.guesses
                    |> List.head
                    |> Maybe.map .feedback
                    |> Expect.equal (Just [ Just Absent, Nothing, Nothing, Nothing, Nothing ])
        , test "validationError requires selected feedback for entered guesses" <|
            \_ ->
                modelWithGuesses [ rawGuess 1 "crane" [ Nothing, Nothing, Nothing, Nothing, Nothing ] ]
                    |> Main.validationError
                    |> Expect.equal "Select feedback for each entered guess."
        , test "AddGuess is ignored while entered feedback is unselected" <|
            \_ ->
                let
                    startingModel =
                        modelWithGuesses [ rawGuess 1 "crane" [ Nothing, Nothing, Nothing, Nothing, Nothing ] ]

                    ( model, _ ) =
                        Main.update AddGuess startingModel
                in
                { guesses = List.length model.guesses
                , nextId = model.nextId
                }
                    |> Expect.equal
                        { guesses = 1
                        , nextId = 2
                        }
        , test "AddGuess is ignored until the current row has a guess and feedback" <|
            \_ ->
                let
                    ( model, _ ) =
                        Main.update AddGuess Main.initialModel
                in
                { guesses = List.length model.guesses
                , nextId = model.nextId
                }
                    |> Expect.equal
                        { guesses = 1
                        , nextId = 2
                        }
        , test "hardModeError requires green letters in subsequent completed guesses" <|
            \_ ->
                hardModeModel
                    [ guess 1 "crane" [ Correct, Absent, Absent, Absent, Absent ]
                    , guess 2 "sloth" [ Absent, Absent, Absent, Absent, Absent ]
                    ]
                    |> Main.hardModeError
                    |> Expect.equal "Guess 'sloth' must use 'c' in position 1."
        , test "hardModeError requires yellow letters in subsequent completed guesses" <|
            \_ ->
                hardModeModel
                    [ guess 1 "crane" [ Present, Absent, Absent, Absent, Absent ]
                    , guess 2 "sloth" [ Absent, Absent, Absent, Absent, Absent ]
                    ]
                    |> Main.hardModeError
                    |> Expect.equal "Guess 'sloth' must include 'c'."
        , test "hardModeError waits for a subsequent guess to be complete" <|
            \_ ->
                hardModeModel
                    [ guess 1 "crane" [ Correct, Absent, Absent, Absent, Absent ]
                    , guess 2 "slo" [ Absent, Absent, Absent, Absent, Absent ]
                    ]
                    |> Main.hardModeError
                    |> Expect.equal ""
        , test "Solve with a hard mode violation shows validation without loading" <|
            \_ ->
                let
                    ( model, _ ) =
                        hardModeModel
                            [ guess 1 "crane" [ Correct, Absent, Absent, Absent, Absent ]
                            , guess 2 "sloth" [ Absent, Absent, Absent, Absent, Absent ]
                            ]
                            |> Main.update Solve
                in
                { error = model.error
                , loading = model.loading
                }
                    |> Expect.equal
                        { error = "Guess 'sloth' must use 'c' in position 1."
                        , loading = False
                        }
        , test "AddGuess is ignored while a hard mode violation exists" <|
            \_ ->
                let
                    startingModel =
                        hardModeModel
                            [ guess 1 "crane" [ Correct, Absent, Absent, Absent, Absent ]
                            , guess 2 "sloth" [ Absent, Absent, Absent, Absent, Absent ]
                            ]

                    ( model, _ ) =
                        Main.update AddGuess startingModel
                in
                { guesses = List.length model.guesses
                , nextId = model.nextId
                }
                    |> Expect.equal
                        { guesses = 2
                        , nextId = 2
                        }
        ]


initialModelWithGuess : String -> Main.Model
initialModelWithGuess word =
    let
        model =
            Main.initialModel
    in
    { model
        | guesses =
            [ { id = 1
              , guess = word
              , feedback = List.map Just [ Absent, Absent, Absent, Absent, Absent ]
              }
            ]
    }


modelWithGuesses : List Main.Guess -> Main.Model
modelWithGuesses guesses =
    let
        model =
            Main.initialModel
    in
    { model | guesses = guesses }


hardModeModel : List Main.Guess -> Main.Model
hardModeModel guesses =
    let
        model =
            Main.initialModel
    in
    { model | guesses = guesses, hardMode = True }


guess : Int -> String -> List Feedback -> Main.Guess
guess id word feedback =
    { id = id
    , guess = word
    , feedback = List.map Just feedback
    }


rawGuess : Int -> String -> List (Maybe Feedback) -> Main.Guess
rawGuess id word feedback =
    { id = id
    , guess = word
    , feedback = feedback
    }
