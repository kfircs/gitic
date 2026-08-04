Status: Done

# Refactoring Plan

## Phase 0: Baseline Tests (Characterization)
- [x] `src/Cli/CliCommandFactory.cs` has test coverage. (Completed)

## Phase 1: Structural Changes (Architecture)
- [x] Extract `HelpCommand` to `src/Cli/Commands/HelpCommand.cs`. (Completed)
- [x] Extract `VersionCommand` to `src/Cli/Commands/VersionCommand.cs`. (Completed)
- [x] Extract `ConfigCommand` to `src/Cli/Commands/ConfigCommand.cs`. (Completed)

## Phase 2: Interface Deepening (Design)
- [x] Simplify CLI abstractions in `BaseAnalysisCommand`. (Completed)

## Phase 3: Local Cleanups (Clean Code)
- [x] Clean up magic strings in `HelpCommand`. (Completed)
