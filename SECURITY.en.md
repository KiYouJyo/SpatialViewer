[简体中文](SECURITY.md) | [日本語](SECURITY.ja.md) | English

# Security Policy

## Supported scope

Spatial Viewer is currently in early development. Security fixes primarily target `main` and the latest public release; historical development snapshots are not maintained independently.

## Reporting a vulnerability

Do not post directly exploitable details, malicious sample files, tokens, private data, or other sensitive information in a public issue.

Use GitHub's private vulnerability reporting channel when it is enabled for the repository. If no private channel is available, open a public issue without exploit details and ask the maintainer for a private contact path.

A useful report should include the affected version or commit, vulnerability type and impact, minimal trigger conditions, whether a crafted file is required, and any known mitigation.

## Crafted files

A viewer processes complex data from external sources. If a vulnerability is triggered by a crafted DWG, DXF, GIS dataset, IFC, 3DM, or other file, do not upload the malicious sample publicly. Share non-sensitive metadata such as file type, trigger path, and hash first, then exchange the sample through a private channel.

Confirmed reports will be assessed and handled according to their impact before coordinated disclosure.
