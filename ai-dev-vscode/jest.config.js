/** @type {import('jest').Config} */
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['**/src/test/**/*.test.ts'],
  moduleNameMapper: { '^vscode$': '<rootDir>/src/__mocks__/vscode.ts' },
  collectCoverageFrom: ['src/**/*.ts', '!src/test/**', '!src/__mocks__/**'],
};
