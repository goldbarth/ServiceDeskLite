import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

// Resolve project root
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const root = path.resolve(__dirname, "..", "..");

// Output directory
const outDir = path.join(root, "docs", "api", "swagger-ui");
fs.mkdirSync(outDir, { recursive: true });

// Resolve swagger-ui-dist location (ESM safe)
import swaggerUiDist from "swagger-ui-dist";
const distPath = swaggerUiDist.getAbsoluteFSPath();

// Files we need
const files = [
    "swagger-ui.css",
    "swagger-ui-bundle.js",
    "swagger-ui-standalone-preset.js"
];

for (const file of files) {
    fs.copyFileSync(
        path.join(distPath, file),
        path.join(outDir, file)
    );
}

console.log("Swagger UI assets copied successfully.");
