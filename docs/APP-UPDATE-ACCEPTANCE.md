# Application update acceptance

SpatialViewer v0.3.2 introduces the in-app MSIX update path ported from UrbanPlanToolbox.

The dedicated `v032-acceptance.yml` workflow validates the released artifacts rather than rebuilding substitutes:

- downloads the official v0.3.1 baseline and official v0.3.2 target from GitHub Releases;
- verifies GitHub asset digest, `SHA256SUMS.txt`, the MSIX signer subject and pinned certificate thumbprint;
- provisions the exact Windows App Runtime 1.8 dependency required by the released package on a clean hosted runner;
- installs official SpatialViewer v0.3.1;
- upgrades it to official SpatialViewer v0.3.2 using `Windows.Management.Deployment.PackageManager.AddPackageAsync` with `DeploymentOptions.ForceApplicationShutdown`;
- verifies the installed package identity, architecture, version, and status after the upgrade;
- removes the test package and temporary trusted certificate at the end of the run.

The first acceptance attempt intentionally exposed a clean-runner dependency prerequisite (`0x80073CF3`, missing `Microsoft.WindowsAppRuntime.1.8`). The final workflow explicitly provisions the exact runtime used by the application before testing the product update path.

Reference acceptance run: `33610839612`.
