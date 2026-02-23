export default [
  {
    files: ["playwright.config.ts", "scripts/**/*.{js,mjs,cjs,ts,mts,cts}"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        process: "readonly"
      }
    }
  },
  {
    files: ["**/*.{js,mjs,cjs,ts,mts,cts}"],
    ignores: [
      "**/bin/**",
      "**/obj/**",
      "node_modules/**",
      "playwright-report/**",
      "test-results/**",
      "**/wwwroot/lib/**",
      "**/wwwroot/_framework/**",
      "**/*.min.js"
    ],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module"
    },
    rules: {
      "no-unused-vars": ["error", { "argsIgnorePattern": "^_" }],
      "no-undef": "error"
    }
  }
];
