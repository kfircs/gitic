# Contributing to Gitic

First off, thank you for your interest in contributing to Gitic! We are excited to welcome your help in making Gitic the best strategic codebase analysis tool available.

As a contributor, please take a moment to review this document to understand our development workflow, engineering standards, and community guidelines.

## Code of Conduct

By participating in this project, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md). Please report any violations privately to the repository maintainers.

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- Git

### Initial Setup
1. Fork and clone the repository:
   ```bash
   git clone https://github.com/your-username/gitic.git
   cd gitic
   ```
2. Restore dependencies:
   ```bash
   dotnet restore Gitic.slnx
   ```
3. Run the unit test suite to verify your setup is fully working:
   ```bash
   dotnet test Gitic.slnx --nologo
   ```

## Development Workflow

1. **Create a branch** for your feature or bugfix:
   ```bash
   git checkout -b my-feature-branch
   ```
2. **Make your changes** following our engineering standards (see below).
3. **Write tests**! All features and bug fixes must have corresponding automated unit tests. Gitic has a highly comprehensive test suite (160+ tests) that must stay green.
4. **Verify your changes**:
   ```bash
   dotnet test Gitic.slnx --nologo
   ```
5. **Install and run locally** to test in action:
   ```bash
   ./install.sh
   gitic
   ```
6. **Submit a Pull Request** to the `main` branch.

## Engineering Standards

- **UNIX Philosophy**: Gitic is designed to be lightweight, incredibly fast (<50ms startup), and composable. Avoid adding heavy startup dependencies or complex interactive elements that block scripting.
- **Type Safety**: Avoid hacks or suppressing linter/compiler warnings. We use nullable reference types (`#nullable enable`) across the codebase.
- **Pure Domain Logic**: Keep analysis logic in `AnalysisPipeline` pure, stateless, and fully separated from I/O and Git interactions.
- **Performance**: Gitic is highly optimized. Ensure your changes do not introduce performance regressions or high memory footprints.

## Code Review Process

All PRs require review and approval from at least one repository maintainer. We check for:
- Code correctness and stylistic consistency.
- Test coverage (unit and contract tests).
- Build and test pass across Ubuntu, Windows, and macOS (via our automated CI Gates).
