import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const layerDir = dirname(fileURLToPath(import.meta.url));

/**
 * Glassmorphism design-system layer for the Callora shells: SCSS tokens on
 * top of the --callora-* workspace theme variables plus the Glass* component
 * set. Consumed via `extends` by both shells.
 */
export default defineNuxtConfig({
  alias: {
    "#shell-design": layerDir
  },
  css: [join(layerDir, "assets/scss/main.scss")],
  vite: {
    css: {
      preprocessorOptions: {
        scss: {
          additionalData: `@use "${join(layerDir, "assets/scss/abstracts")}" as *;\n`
        }
      }
    }
  }
});
