import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const elmProject = join(repoRoot, "WordleSolver.Elm");
const committedClient = join(repoRoot, "WordleSolver", "wwwroot", "app.js");
const tempDir = mkdtempSync(join(tmpdir(), "wordle-client-check-"));
const tempClient = join(tempDir, "app.js");

const elm = spawnSync(
  "elm",
  ["make", "src/Main.elm", "--optimize", "--output", tempClient],
  {
    cwd: elmProject,
    encoding: "utf8",
    shell: process.platform === "win32",
  },
);

try {
  if (elm.status !== 0) {
    process.stdout.write(elm.stdout);
    process.stderr.write(elm.stderr);
    process.exit(elm.status ?? 1);
  }

  const current = readFileSync(committedClient);
  const rebuilt = readFileSync(tempClient);

  if (!current.equals(rebuilt)) {
    console.error(`
WordleSolver/wwwroot/app.js is stale relative to WordleSolver.Elm.

Run:
  npm run build:client

Then commit the updated WordleSolver/wwwroot/app.js.
`);
    process.exit(1);
  }

  console.log("WordleSolver/wwwroot/app.js is current.");
} finally {
  rmSync(tempDir, { recursive: true, force: true });
}
