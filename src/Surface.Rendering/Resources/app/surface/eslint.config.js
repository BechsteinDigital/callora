// Statisches Gate neben vue-tsc (#297): Der Typprüfer sagt, ob die Typen stimmen, nicht ob der
// Code stimmt. Die beiden Suiten hatten bisher nichts, was Letzteres prüft.
//
// Der Regelsatz ist bewusst der Fehlerklasse verpflichtet und nicht dem Geschmack: `recommended`
// von ESLint und typescript-eslint plus Vues `essential`. Was hier anschlägt, ist ein Befund und
// keine Formatierungsfrage — eine Regel, über die man diskutieren kann, macht ein Gate zu einer
// Meinung, und Meinungen schaltet man irgendwann ab.
import js from '@eslint/js'
import globals from 'globals'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      'dist-lib/**',
      'node_modules/**',
      'src/nunjucks.js',
      // Absichtlich fremder Code: Die Fixture ist ein Plugin-Bundle, wie es von außen kommt, und
      // soll genau so aussehen. Nach unseren Regeln zu formen hieße, am Testgegenstand zu drehen.
      'src/__fixtures__/**',
    ],
  },
  js.configs.recommended,
  tseslint.configs.recommended,
  pluginVue.configs['flat/essential'],
  {
    files: ['**/*.{ts,vue}'],
    languageOptions: {
      globals: { ...globals.browser },
      parserOptions: { parser: tseslint.parser },
    },
  },
  {
    // Node-Umgebung statt Browser: Was hier läuft, läuft beim Bauen, nicht in einer Seite.
    files: ['scripts/**/*.{js,mjs}', '*.config.{js,ts}', 'src/build-constants.ts'],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    // Testdateien dürfen `any` gegen Fremdtypen setzen, wo ein Fake sonst mehr Vertrag nachbauen
    // müsste, als er prüft.
    files: ['**/*.spec.ts', '**/*.test.ts'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      // Ein Stub in einem Test heißt "Stub" und nicht "TheTestStubComponent".
      'vue/multi-word-component-names': 'off',
    },
  },
)
