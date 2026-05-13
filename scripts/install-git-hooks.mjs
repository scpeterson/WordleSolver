import { chmodSync, copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const source = join(repoRoot, "scripts", "git-hooks", "pre-push");
const targetDir = join(repoRoot, ".git", "hooks");
const target = join(targetDir, "pre-push");

if (!existsSync(join(repoRoot, ".git"))) {
  console.error("Cannot install hooks because .git was not found.");
  process.exit(1);
}

mkdirSync(targetDir, { recursive: true });
copyFileSync(source, target);
chmodSync(target, 0o755);

console.log("Installed .git/hooks/pre-push.");
