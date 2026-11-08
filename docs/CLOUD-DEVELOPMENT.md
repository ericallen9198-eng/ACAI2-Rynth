# Cloud development

Use this checkout independently. Do not create a worktree or change the sibling
Samm-AI project unless separately requested. Initialize the pinned public RynthCore
submodule with `git submodule update --init --recursive`.

This scaffold was checked with a checksum-verified .NET 10.0.403 SDK, installed
outside the repository at `/workspace/.dotnet10`. Original ACAI2 tooling remains at
`/workspace/.dotnet` (.NET 8). Standard future environments can use any matching
.NET 10 SDK selected by global.json; no SDK binary is committed here.

For this retained cloud workspace, activate the new SDK for this command only:

```bash
PATH=/workspace/.dotnet10:$PATH \
DOTNET_ROOT=/workspace/.dotnet10 \
DOTNET_CLI_HOME=/workspace/.setup/dotnet10-home \
NUGET_PACKAGES=/workspace/.setup/nuget \
scripts/test.sh
```

.NET 10 named mutex setup initially failed because the sandbox user's home is
read-only. A writable user-owned `XDG_RUNTIME_DIR` fixes that without changing HOME
or sandbox policy. `scripts/test.sh` supplies one when absent. Preserve a configured
runtime directory. No new runtime service or production Bridge is required.

Linux compilation against the real SDK succeeds with one expected upstream
warning: plugin manifest embedding uses Windows UpdateResource. Do not suppress
that warning or distribute the Linux output as a fully validated Windows package.
CI includes a separate Windows publish job, but its status must be checked on
GitHub rather than inferred from the local Linux run.
