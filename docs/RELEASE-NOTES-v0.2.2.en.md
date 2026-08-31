[简体中文](RELEASE-NOTES-v0.2.2.md) | [日本語](RELEASE-NOTES-v0.2.2.ja.md) | English

# Spatial Viewer v0.2.2

This release focuses on the runtime failure seen after `SpatialViewer.CadCore` successfully discovers a newer version but fails while downloading or staging it in a real installed environment. It does not redesign the already accepted viewer UI.

## CadCore updater reliability

- GitHub Release asset downloads now retry through the system proxy path and automatically fall back to a direct connection when the proxy route is unavailable, returns incomplete content, or fails verification.
- Every download attempt validates both the GitHub-reported file size and SHA-256 digest; only a complete, digest-matching kernel archive can proceed to extraction and staging.
- Validation of `cadcore-release.json`, x64 architecture, source repository, SpatialViewer 0.2.x compatibility, and the versions of all five core assemblies remains mandatory.
- Updates still use the safe “stage → user manually closes and reopens the app → activate the new kernel” flow. Assemblies are never hot-swapped in the running process.

## Diagnosable failures

- Runtime failures are no longer collapsed into one generic “kernel update failed” message. Timeout, network, SHA-256, storage access, package validation, and version mismatch stages retain distinct error codes.
- Detailed failures are written to `update.log` under the user kernel directory; the About page keeps a compact status while exposing the detailed error through a tooltip.
- If a machine-specific problem remains, the failure stage can be identified directly instead of guessing whether download, validation, or local staging failed.

## Stronger acceptance

- The signed acceptance workflow now downloads the live `SpatialViewer.CadCore` Release using the updater-style proxy retry / direct fallback strategy and verifies its exact size and SHA-256.
- It then extracts the package, validates the manifest and five core DLLs, and simulates the complete `versions/<version>` plus `pending.json` staging path.
- v0.2.2 acceptance artifacts are produced only after trilingual resources, Debug/Release builds, tests, startup smoke, live Release contract, runtime download/staging, MSIX generation, signing, and offline acceptance packaging all pass.

> v0.2.2 intentionally keeps bundled CadCore v0.2.0 as the fallback baseline so the independently published CadCore v0.2.1 remains a real update that can be discovered and downloaded during validation.