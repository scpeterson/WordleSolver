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
                                [ { id = 1, guess = "", feedback = [ Absent, Absent, Absent, Absent, Absent ] }
                                , { id = 2, guess = "crane", feedback = [ Correct, Present, Absent, Absent, Absent ] }
                                ]
                        }
                in
                model
                    |> Main.enteredGuesses
                    |> List.map .guess
                    |> Expect.equal [ "crane" ]
        ]


initialModelWithGuess : String -> Main.Model
initialModelWithGuess guess =
    let
        model =
            Main.initialModel
    in
    { model
        | guesses =
            [ { id = 1
              , guess = guess
              , feedback = [ Absent, Absent, Absent, Absent, Absent ]
              }
            ]
    }
