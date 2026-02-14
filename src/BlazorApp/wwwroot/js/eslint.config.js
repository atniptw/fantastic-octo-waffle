import sonarjs from "eslint-plugin-sonarjs";

export default [
  {
    files: ["*.js"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        window: "readonly",
        document: "readonly",
        console: "readonly",
        requestAnimationFrame: "readonly",
        cancelAnimationFrame: "readonly"
      }
    },
    plugins: {
      sonarjs
    },
    rules: {
      "sonarjs/cognitive-complexity": ["error", 10],
      "sonarjs/no-duplicate-string": "warn",
      "sonarjs/no-identical-functions": "warn",
      "complexity": ["warn", 10],
      "max-depth": ["warn", 4],
      "max-lines-per-function": ["warn", { "max": 50, "skipBlankLines": true, "skipComments": true }]
    }
  }
];
