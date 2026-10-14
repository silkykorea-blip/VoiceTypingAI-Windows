# Windows Input Protocol Test Fixtures

`android829-input.json` is the protocol baseline used by the Windows offline regression tests. It contains prompts, tool schemas, and verification metadata for the source version. It does not contain Android application source code, user recordings, or API keys.

Although the filename retains the name of the baseline's origin, this directory belongs to the Windows project's tests. `DesktopRiskTests.cs` reads the fixture to detect unintended changes to the voice typing and translation input protocols.

Do not add real user input, private logs, or credentials to this directory. Provider tests that require audio should use a non-sensitive file supplied by the person running the test and kept outside the repository.
