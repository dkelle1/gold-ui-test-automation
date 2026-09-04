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
    // fixtures-app/server.mjs and this config file are plain ES modules run by Node directly, not
    // covered by typescript-eslint's config and with no Node globals of their own.
    files: ['**/*.{js,mjs,cjs}'],
    languageOptions: {
      globals: {
        process: 'readonly',
        console: 'readonly'
      }
    }
  },
  {
    // The fixture app's inline page scripts run in a browser, not in Node.
    files: ['fixtures-app/public/**/*.js'],
    languageOptions: {
      globals: {
        window: 'readonly',
        document: 'readonly',
        location: 'readonly',
        navigator: 'readonly',
        fetch: 'readonly'
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
