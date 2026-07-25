# Domain Model

## Core Concepts

- **Analysis Pipeline**: A pure, stateless phase of the application that transforms raw `GitCommitRecord`s and configuration into computed metrics (`FileMetric`, `AreaMetric`, etc.) without performing any disk or Git I/O.
