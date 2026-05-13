# WordleSolverNew

An F#/.NET 10 Wordle helper built around pure functional solver logic and a small Elm-style browser UI.

## Open in Rider

Open `WordleSolverNew.sln` in JetBrains Rider. The web project is `WordleSolver/WordleSolver.fsproj`.

## Run

```bash
dotnet run --project WordleSolver/WordleSolver.fsproj
```

Then open the URL printed by ASP.NET Core.

## Environments

The app supports four ASP.NET Core environment names:

- `DEV`
- `QA`
- `STAGE`
- `PROD`

Each environment has a matching `WordleSolver/appsettings.{ENV}.json` file. Rider launch profiles are also defined for each environment. From the command line, run a specific environment like this:

```bash
ASPNETCORE_ENVIRONMENT=QA dotnet run --project WordleSolver/WordleSolver.fsproj
```

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
  "hardMode": true,
  "candidates": ["spine", "horse", "smile", "style"]
}
```

The `candidates` and `hardMode` fields are optional. When `candidates` is omitted, the project uses `WordleSolver/Data/answers.txt`.

When `hardMode` is `true`, every guess after the first must use revealed hints from earlier guesses. Green letters must stay in the same position, and revealed green or yellow letters must be included in later guesses.

## Answer List Provenance

`WordleSolver/Data/answers.txt` is checked into the repository so solver behavior is deterministic. It was sourced from cfreshman's alphabetical original Wordle answer list:

https://gist.github.com/cfreshman/a03ef2cba789d8cf00c08f767e0fad7b

This is the original static answer list, not a live feed of the current New York Times curated Wordle answer pool. Do not refresh it automatically on a schedule. If the answer list needs to change, update it manually in a pull request so the behavior change can be reviewed with the tests.

## Frontend

The browser UI is compiled from Elm source in `WordleSolver.Elm` into the committed asset `WordleSolver/wwwroot/app.js`.

After changing Elm code, rebuild the committed client bundle:

```bash
npm run build:client
```

Before committing, verify the checked-in bundle is current and run the Elm tests:

```bash
npm run check:client
npm run test:client
```

To catch stale generated client files before pushing, install the optional pre-push hook:

```bash
npm run install:hooks
```

## Verify

```bash
dotnet build WordleSolverNew.sln
dotnet test WordleSolverNew.sln
```
