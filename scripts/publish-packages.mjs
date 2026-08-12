import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createInterface } from "readline/promises";
import { stdin as input, stdout as output } from "process";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dryRun = process.argv.includes("--dry-run");
const skipPublish = process.argv.includes("--skip-publish");
const rl = createInterface({ input, output });

const packages = [
  ["AventusSharp.Core", "AventusSharp.Core/AventusSharp.Core.csproj"],
  ["AventusSharp.AspNetCore", "AventusSharp.AspNetCore/AventusSharp.AspNetCore.csproj"],
  ["AventusSharp.Maui", "AventusSharp.Maui/AventusSharp.Maui.csproj"],
  ["AventusSharp.Data.Sqlite", "AventusSharp.Data.Sqlite/AventusSharp.Data.Sqlite.csproj"],
  ["AventusSharp.Data.Mysql", "AventusSharp.Data.Mysql/AventusSharp.Data.Mysql.csproj"],
  ["AventusSharp.Data.Postgresql", "AventusSharp.Data.Postgresql/AventusSharp.Data.Postgresql.csproj"],
  ["AventusSharp.Data.Mssql", "AventusSharp.Data.Mssql/AventusSharp.Data.Mssql.csproj"],
  ["AventusSharp.Converter", "CSharpToTypescript/CSharpToTypescript.csproj"],
  ["AventusSharp.DatabaseQuery", "DatabaseQuery/DatabaseQuery.csproj"],
];

const packageJson = JSON.parse(readFileSync(join(repositoryRoot, "package.json")));
const currentVersion = packageJson.version;

if (!currentVersion || !/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(currentVersion)) {
  console.error("Can't find the version inside package.json");
  process.exit(1);
}

const [major, minor, patch] = currentVersion.split("-")[0].split(".").map(Number);
const newVersion = `${major}.${minor}.${patch + 1}`;
const answer = await rl.question(`La version actuelle est ${currentVersion}. Augmenter vers ${newVersion} ? (y/n) `);
const version = answer.toLowerCase().trim() === "y" ? newVersion : currentVersion;

if(newVersion != currentVersion) {
  packageJson.version = newVersion;
  writeFileSync(join(repositoryRoot, "package.json"), JSON.stringify(packageJson, undefined, 4))
}

function run(command, args) {
  console.log(`> ${command} ${args.join(" ")}`);
  if (dryRun) return;

  const result = spawnSync(command, args, {
    cwd: repositoryRoot,
    stdio: "inherit",
    // dotnet is an executable and does not need a shell. The custom
    // dotnet-publish command may be a .cmd file on Windows.
    shell: process.platform === "win32" && command === "dotnet-publish"
  });
  if (result.status !== 0) {
    throw new Error(`${command} failed with exit code ${result.status ?? "unknown"}`);
  }
}

const originals = new Map();
const artifactsDirectory = join(repositoryRoot, "artifacts", "packages");

try {
  for (const [, relativeProject] of packages) {
    const project = join(repositoryRoot, relativeProject);
    const content = readFileSync(project, "utf8");
    if (!/<Version>[^<]+<\/Version>/.test(content)) {
      throw new Error(`No <Version> element found in ${relativeProject}`);
    }
    originals.set(project, content);
    if (!dryRun) {
      writeFileSync(
        project,
        content.replace(/<Version>[^<]+<\/Version>/, `<Version>${version}</Version>`),
        "utf8"
      );
    }
  }

  if (!dryRun) {
    rmSync(artifactsDirectory, { recursive: true, force: true });
    mkdirSync(artifactsDirectory, { recursive: true });
  }

  run("dotnet", ["build", "AventusSharp.sln", "--configuration", "Release"]);

  for (const [, relativeProject] of packages) {
    run("dotnet", [
      "pack",
      relativeProject,
      "--configuration", "Release",
      "--no-build",
      "--output", artifactsDirectory
    ]);
  }

  if (!skipPublish) {
    for (const [packageId] of packages) {
      const packagePath = join(artifactsDirectory, `${packageId}.${version}.nupkg`);
      if (!dryRun && !existsSync(packagePath)) {
        throw new Error(`Package not found: ${packagePath}`);
      }
      run("dotnet-publish", [packagePath]);
    }
  }

  console.log(
    dryRun
      ? `Dry run successful for version ${version}.`
      : skipPublish
        ? `Packages ${version} built without publication.`
        : `All AventusSharp packages ${version} published.`
  );

  rl.close();
} catch (error) {
  if (!dryRun) {
    for (const [project, content] of originals) {
      writeFileSync(project, content, "utf8");
    }
  }
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
}
