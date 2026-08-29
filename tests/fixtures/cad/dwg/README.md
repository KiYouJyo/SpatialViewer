# Generated DWG fixtures

The CAD test suite creates each DWG fixture at test time from the adjacent, self-authored DXF fixture through `ACadSharpFixtureTranscoder`. This keeps the repository free of opaque binary blobs and ensures the DXF/DWG semantic-parity tests use the same redistributable drawing content.
