[简体中文](CONTRIBUTING.md) | [日本語](CONTRIBUTING.ja.md) | English

# Contributing

Thank you for your interest in Spatial Viewer.

## Before opening an issue

- Search existing issues first.
- For format-compatibility problems, include the file format, source application and version, expected result, and actual result where possible.
- Screenshots are welcome, but remove usernames, local paths, project/client information, sensitive coordinates, and other private data.
- Only share sample files that you have the right to disclose and redistribute. Do not upload engineering files restricted by NDAs, commercial licenses, or privacy obligations.

## Code contributions

1. Create a focused branch from `main`.
2. Keep each pull request scoped to one clear problem where practical.
3. Keep format reading, parsing, rendering, and UI concerns separated. Avoid adding format-specific exceptions to shared layers unless they are truly reusable.
4. New user-facing text should account for Simplified Chinese, Japanese, and English resources.
5. New libraries, SDKs, fonts, icons, test data, or other external assets must have compatible licenses; update third-party notices where applicable.
6. Never commit private keys, signing certificates, tokens, account data, or real sensitive project files.

## Compatibility and test data

Viewer compatibility depends heavily on real files. A useful compatibility report should identify the file/version, producing application/version, minimal reproduction steps, relevant layer/entity/CRS/font/material conditions, and the before/after behavior.

If an original file cannot be published, create a minimal sanitized reproduction instead.

## Pull requests

Describe the purpose, main changes, validation performed, and known limitations. Include screenshots for UI changes and list representative sample types for format-related changes.

By contributing, you confirm that you have the right to submit the contribution and agree that it is licensed under this repository's [MIT License](LICENSE).

Please also follow the [Code of Conduct](CODE_OF_CONDUCT.en.md).
