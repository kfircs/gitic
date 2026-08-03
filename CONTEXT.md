# Domain Model

## Core Concepts

- **Analysis Pipeline**: A pure, stateless phase of the application that transforms raw `GitCommitRecord`s and configuration into computed metrics (`FileMetric`, `AreaMetric`, etc.) without performing any disk or Git I/O.

## Core Architecture Pillars

The codebase is organized under `src/` into 6 cohesive, deep modules:

1. **Core (`src/Core/`)**: Contains the pure, stateless metrics engine, the analysis pipeline, scoring algorithms, and core domain models.
2. **Config (`src/Config/`)**: Manages reading, parsing (YAML), normalizing, and validating local settings (`.gitic.yml`) and override options.
3. **Git (`src/Git/`)**: Handles subprocess-level integration with the Git CLI and parsing of git log and patch streams into structured records.
4. **Cli (`src/Cli/`)**: Handles command-line arguments parsing, interactive TUI/Wizard execution, and progress reporting.
5. **Reporting (`src/Reporting/`)**: Takes metrics and formats them into diverse output formats (HTML dashboards, SVGs, Console Tables, Markdown, and JSON).
6. **Utilities (`src/Utilities/`)**: Houses reusable helper functions such as file/glob path pattern matching.
