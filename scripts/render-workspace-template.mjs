#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const nunjucks = require("nunjucks");

function parseArgs(argv) {
  const args = {
    input: "",
    output: "",
    templatesRoot: "",
    locale: "en-US",
    currency: "USD"
  };

  for (let i = 2; i < argv.length; i += 1) {
    const key = argv[i];
    const value = argv[i + 1];

    switch (key) {
      case "--input":
        args.input = value || "";
        i += 1;
        break;
      case "--output":
        args.output = value || "";
        i += 1;
        break;
      case "--templates-root":
        args.templatesRoot = value || "";
        i += 1;
        break;
      case "--locale":
        args.locale = value || "en-US";
        i += 1;
        break;
      case "--currency":
        args.currency = value || "USD";
        i += 1;
        break;
      default:
        break;
    }
  }

  return args;
}

function fail(message) {
  process.stderr.write(`${message}\n`);
  process.exit(1);
}

const args = parseArgs(process.argv);
if (!args.input || !args.output || !args.templatesRoot) {
  fail("Usage: render-workspace-template.mjs --input <file> --output <file> --templates-root <dir> [--locale <locale>] [--currency <currency>]");
}

const templatesRoot = path.resolve(args.templatesRoot);
const inputPath = path.resolve(args.input);
const outputPath = path.resolve(args.output);

if (!fs.existsSync(inputPath)) {
  fail(`Input template not found: ${inputPath}`);
}

const relativeTemplatePath = path.relative(templatesRoot, inputPath).replace(/\\/g, "/");
if (relativeTemplatePath.startsWith("..")) {
  fail(`Input template must be inside templates root. input=${inputPath} root=${templatesRoot}`);
}

const env = new nunjucks.Environment(
  new nunjucks.FileSystemLoader(templatesRoot, { noCache: true }),
  { autoescape: false, throwOnUndefined: false }
);

env.addFilter("currency", (value, currencyCode = args.currency, locale = args.locale) => {
  const numericValue = Number(value);
  if (!Number.isFinite(numericValue)) {
    return "";
  }

  try {
    return new Intl.NumberFormat(locale, {
      style: "currency",
      currency: String(currencyCode || args.currency || "USD")
    }).format(numericValue);
  } catch {
    return String(numericValue);
  }
});

env.addFilter("split", (value, separator = ",") => {
  if (value === null || value === undefined) {
    return [];
  }

  return String(value).split(String(separator));
});

const rendered = env.render(relativeTemplatePath, {});
fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, rendered, { encoding: "utf-8" });
