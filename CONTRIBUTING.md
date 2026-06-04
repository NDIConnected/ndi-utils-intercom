# Contributing

Thank you for your interest in NDI Intercom.

## Development setup

**Requirements**

- Windows 10/11 x64 (primary target) or Linux (experimental)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [NDI 6 SDK](https://ndi.video/) installed on the build machine
- Optional: [Inno Setup 6](https://jrsoftware.org/isinfo.php) for Windows installers

**Build**

```bash
dotnet build "NDI-Intercom.sln" -c Release
```

**Publish & installers**

- Intercom16: `build-intercom16-installer.cmd`
- Intercom2: `build-intercom2-installer.cmd`

See [README.md](README.md) for full details.

## Pull requests

1. Fork the repository and create a feature branch from `main`.
2. Keep changes focused — one logical change per PR.
3. Ensure `dotnet build "NDI-Intercom.sln" -c Release` succeeds.
4. Update [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
5. Open a PR against `NDIConnected/ndi-utils-intercom` with a clear description and test notes.

## Code style

- Match existing naming and patterns in the file you edit.
- Avoid LINQ and allocations in real-time audio callbacks.
- Do not commit the NDI SDK tree, build output (`bin/`, `obj/`, `publish/`), or secrets.

## Reporting issues

Use [GitHub Issues](https://github.com/NDIConnected/ndi-utils-intercom/issues). Include product (Intercom16 / Intercom2), version, OS, and steps to reproduce.

**Security vulnerabilities** must not be posted as public issues. Use [GitHub Security Advisories](https://github.com/NDIConnected/ndi-utils-intercom/security/advisories/new) instead. See [SECURITY.md](SECURITY.md).

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

## License

By contributing, you agree that your contributions will be licensed under the same [MIT License](LICENSE) as the project.
