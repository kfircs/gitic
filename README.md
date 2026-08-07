# Gitic 🚀

**Gitic** is a lightning-fast, highly optimized codebase analysis tool for Git repositories. Built on **.NET 10**, it analyzes commit history, author dynamics, directory ownership, and file changes to identify code hotspots, contributor profiles, silos, and temporal coupling.

Running `gitic` boots directly into a beautiful, interactive **TUI (Terminal User Interface) Dashboard & Wizard**, letting you analyze and generate customized codebase health diagnostics interactively. For automation, it supports non-interactive triggers (`--json`, `--html`, `--md`) making it fully composable with your CI/CD pipelines and Unix shell chains.

---

## Key Capabilities

- 📊 **Polymorphic Interactive TUI**: Explore your codebase through dedicated, hot-swappable perspectives (Lines & Structure, Work Classification, Code Rot, Review Collaboration, AI Code Strain, and logical Areas).
- 🔥 **Hotspots & Risk Density**: Pinpoint high-churn, high-complexity files and measure **Hotspot Risk Density** (the ratio of high-risk files in a logical subsystem).
- 📂 **Architectural Area Ownership & Silos**: Drill down into code ownership, active authors, and subsystem-level **Key-Person Silo Risks** (percentage of files with a single contributor).
- 👥 **Review Collaboration & Team Dynamics**: Build deep contributor profiles and analyze peer-review networks, review cliques, and former employee "orphaned" code.
- 🔄 **Temporal Coupling Networks**: Locate **Coupling Hubs** (files acting as change multipliers) and compute **Cross-Boundary Crossing Rates** to identify architectural leaks.
- ⏱️ **Delivery Velocity (P50/P90)**: Use linear interpolation percentiles to measure typical delivery times (**P50 Median**) and worst-case process bottlenecks (**P90 Tail Latency**).
- 🌐 **Rich Visual Reporting**: Render stunning standalone visual HTML reports, Markdown summaries, and embedded SVGs directly from your terminal.

---

## 💾 Installation

### Option A: Install as a Global .NET Tool (Recommended)
If you have the .NET SDK installed, simply run:
```bash
dotnet tool install -g gitic
```
To run the interactive TUI anywhere, simply type:
```bash
gitic
```

### Option C: Standalone One-Click Installer (No .NET required!)
For teams or containers without the .NET SDK, download and install the precompiled, self-contained single-file native executable with a single `curl` command:
```bash
curl -sSL https://raw.githubusercontent.com/your-username/gitic/main/scripts/install-release.sh | bash
```
This automatically detects your operating system (macOS or Linux) and CPU architecture (Intel or Apple Silicon/ARM) to download the correct optimized single-file binary.

---

## 🛠️ Usage

### Interactive TUI Mode
Simply run the tool inside any Git repository:
```bash
gitic
```
You will boot directly into the interactive dashboard. Use the following keyboard controls to explore high-value insights in real-time:
* **`j` / `k` (or `↑` / `↓`):** Move selection up and down the navigation tree.
* **`l` / `Enter` (or `→`):** Drill down into directories, logical subsystems, or contributor details.
* **`h` / `Backspace` (or `←`):** Go back up one level.
* **`Tab` (or `1` - `5`):** Hot-swap between active perspectives (**Lines & Structure**, **Work Classification**, **Code Rot / Zombies**, **Review Collaboration**, and **AI Code Strain**).
* **`?`:** Display an interactive metric overlay modal explaining P90, Hotspot Densities, and Stall Rates.
* **`/`:** Open the real-time search/filter panel to narrow down the active workspace tree.
* **`q`:** Quit the TUI cleanly.

### Non-Interactive & Automation Mode
Gitic respects standard UNIX redirection. If stdout or stdin is redirected (or if specified via command-line flags), it bypasses the interactive TUI and runs in high-speed, non-interactive mode.

#### 1. Generate visual report files directly
```bash
gitic . --html ./report.html --md ./summary.md --svg ./assets/
```

#### 2. Output analysis data in raw JSON format
```bash
gitic /path/to/my-repo --json
```

#### 3. Filter history using date or directory options
```bash
gitic . --since "2026-01-01" --until "2026-06-30" --depth 4 --json
```

---

## ⚙️ Configuration (.gitic.yml)

Gitic supports local overrides to exclude files (e.g. `node_modules/**`, third-party assets) and define custom attention scoring metrics. To generate a starter configuration file, choose the starter config option in the TUI, or run Gitic with `.gitic.yml` in your repository root:

```yaml
identity:
  merge_on_email: true

metrics:
  temporal_coupling_min_degree: 0.35
  temporal_coupling_max_commit_file_count: 50

excludes:
  - pattern: "**/node_modules/**"
    category: dependency
  - pattern: "**/bin/**"
    category: build
  - pattern: "**/obj/**"
    category: build
```

---

## 🤝 Contributing

We welcome contributions of all kinds! Please see our [Contributing Guide](CONTRIBUTING.md) to set up your environment, write unit tests, and maintain architectural standards.

## 📄 License

Gitic is licensed under the [MIT License](LICENSE).
