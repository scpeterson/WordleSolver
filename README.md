# WordleSolverNew

An F#/.NET 10 Wordle helper built around pure functional solver logic and a small Elm-style browser UI.

## Open in Rider

Open `WordleSolverNew.sln` in JetBrains Rider. The web project is `WordleSolver/WordleSolver.fsproj`.

## Run

```bash
dotnet run --project WordleSolver/WordleSolver.fsproj
```

Then open the URL printed by ASP.NET Core.

## Feedback Tokens

The API accepts five feedback tokens per guess:

- `G` or `C`: correct letter in the correct position
- `Y` or `P`: correct letter in the wrong position
- `B`, `A`, or `X`: absent letter

Example: if the answer is `spine` and the guess is `house`, Wordle feedback is `BBBYG`. The solver filters by replaying standard Wordle scoring against every candidate, so a yellow `s` in the fourth guessed position excludes words with `s` in that same fourth position.

## API

`POST /api/solve`

```json
{
  "guesses": [
    { "guess": "house", "feedback": "BBBYG" }
  ],
  "candidates": ["spine", "horse", "smile", "style"]
}
```

The `candidates` field is optional. When omitted, the project uses `WordleSolver/Data/answers.txt`, sourced from cfreshman's alphabetical original Wordle answer list:

https://gist.github.com/cfreshman/a03ef2cba789d8cf00c08f767e0fad7b

## Verify

```bash
dotnet build WordleSolverNew.sln
dotnet test WordleSolverNew.sln
```
