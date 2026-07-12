import { fileURLToPath } from "node:url";

export default defineNuxtConfig({
  alias: {
    "#shell-core": fileURLToPath(new URL(".", import.meta.url))
  }
});
