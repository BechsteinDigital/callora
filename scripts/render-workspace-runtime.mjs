#!/usr/bin/env node
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const nunjucks = require("nunjucks");

function fail(message) {
  process.stderr.write(`${message}\n`);
  process.exit(1);
}

let input = "";
process.stdin.setEncoding("utf-8");
process.stdin.on("data", (chunk) => {
  input += chunk;
});

process.stdin.on("end", () => {
  try {
    const payload = JSON.parse(input || "{}");
    const template = typeof payload.template === "string" ? payload.template : "";
    const model = payload.model && typeof payload.model === "object" ? payload.model : {};

    const env = new nunjucks.Environment(undefined, {
      autoescape: false,
      throwOnUndefined: false
    });

    env.addFilter("currency", (value, currencyCode = "USD", locale = "en-US") => {
      const numericValue = Number(value);
      if (!Number.isFinite(numericValue)) {
        return "";
      }

      try {
        return new Intl.NumberFormat(locale, {
          style: "currency",
          currency: String(currencyCode || "USD")
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

    const html = env.renderString(template, model);
    process.stdout.write(html);
  } catch (error) {
    fail(error instanceof Error ? error.message : String(error));
  }
});

process.stdin.resume();
