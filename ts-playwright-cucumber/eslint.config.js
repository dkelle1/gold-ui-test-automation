import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig([
  globalIgnores(['node_modules/**', 'allure-results/**', 'allure-report/**', 'artifacts/**']),
  js.configs.recommended,
  tseslint.configs.recommended,
  {
    // Plain Node scripts (scripts/summarize-report.mjs, this file, cucumber.js) aren't covered by
    // typescript-eslint's config, and js.configs.recommended has no Node globals of its own - without
    // this, `process`/`console` here would be flagged as undefined rather than pulling in a whole
    // `globals` package dependency for two identifiers.
    files: ['**/*.{js,mjs,cjs}'],
    languageOptions: {
      globals: {
        process: 'readonly',
        console: 'readonly'
      }
    }
  },
  {
    files: ['**/*.ts'],
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      // chai's fluent assertions (`expect(x).to.be.true`, `.to.not.be.null`, ...) are getter accesses
      // whose entire point is the internal side effect of throwing on failure - not "an unused
      // expression" in the sense this rule means to catch. Standard, expected friction between chai and
      // this rule; disabling it here is the conventional fix, not a loosening of real lint coverage.
      '@typescript-eslint/no-unused-expressions': 'off',
      // Parameter properties (`constructor(private readonly x: T)`) need real code generation (an
      // implicit `this.x = x`), not just type erasure - Node's native TypeScript support (which loads
      // this project's files inside a Cucumber `--parallel` worker thread; see cucumber.js's doc
      // comment) only strips erasable syntax and throws ERR_UNSUPPORTED_TYPESCRIPT_SYNTAX on this,
      // confirmed by hitting that exact error. Banned at lint time so it can't regress.
      '@typescript-eslint/parameter-properties': 'error'
    }
  }
]);
