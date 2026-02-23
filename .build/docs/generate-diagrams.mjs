import { execFileSync } from "node:child_process";
import { readdirSync, mkdirSync, existsSync } from "node:fs";
import { join, extname, basename, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = join(__dirname, "..", "..");

const inputDir = join(repoRoot, "docs", "diagrams");
const outputDir = join(repoRoot, "docs", "assets", "diagrams");

if (!existsSync(inputDir)) {
    console.error(`Input dir not found: ${inputDir}`);
    process.exit(1);
}

mkdirSync(outputDir, { recursive: true });

const files = readdirSync(inputDir)
    .filter(f => extname(f).toLowerCase() === ".mmd")
    .sort((a, b) => a.localeCompare(b));

if (files.length === 0) {
    console.log("No .mmd files found. Skipping diagram generation.");
    process.exit(0);
}

// Call the Mermaid CLI JS entry directly via Node (avoids .cmd wrappers on Windows)
const mmdcCli = resolve(__dirname, "node_modules", "@mermaid-js", "mermaid-cli", "src", "cli.js");

if (!existsSync(mmdcCli)) {
    console.error(`Mermaid CLI entry not found. Expected: ${mmdcCli}`);
    process.exit(1);
}

console.log(`Generating ${files.length} diagram(s)...`);

const puppeteerConfig = resolve(__dirname, "puppeteer.config.json");
const mermaidConfig = resolve(__dirname, "mermaid.config.json");

for (const file of files) {
    const inFile = join(inputDir, file);
    const outFile = join(outputDir, `${basename(file, ".mmd")}.svg`);

    execFileSync(
        process.execPath, // node.exe that runs this script
        [
            mmdcCli,
            "-i", inFile,
            "-o", outFile,
            "-b", "white",
            "-c", mermaidConfig,
            "--puppeteerConfigFile", puppeteerConfig
        ],
        { stdio: "inherit" }
    );
}

console.log("Mermaid diagrams generated.");
