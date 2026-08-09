import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig([
  globalIgnores([
    'node_modules/**',
    'allure-results/**',
    'allure-report/**',
    'allure-report-single-file/**',
    'playwright-report/**',
    'test-results/**',
    'artifacts/**'
  ]),
  js.configs.recommended,
  tseslint.configs.recommended,
  {
    // This config file itself is a plain ES module, not covered by typescript-eslint's config.
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
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }]
    }
  }
]);
