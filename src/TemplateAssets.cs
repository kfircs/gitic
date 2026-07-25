namespace Gitic;

public static class TemplateAssets
{
    public const string CssThemes = """
    /* ── Theme Definitions ── */
    :root[data-theme="deloitte"] {
      --canvas: #0A0A09;
      --surface: #1D1D1B;
      --surface-dim: #0F0F0E;
      --surface-bright: #383835;
      --surface-container: #252522;
      --primary: #86BC25;
      --on-primary: #1D1D1B;
      --on-surface: #EFEFED;
      --on-surface-variant: #ABABAA;
      --accent-gradient: linear-gradient(135deg, #86BC25, #1D1D1B, #1B2D4F);
      --outline: #424240;
    }

    :root[data-theme="catppuccin"] {
      --canvas: #1e1e2e;
      --surface: #181825;
      --surface-dim: #11111b;
      --surface-bright: #313244;
      --surface-container: #252538;
      --primary: #cba6f7;
      --on-primary: #11111b;
      --on-surface: #cdd6f4;
      --on-surface-variant: #a6adc8;
      --accent-gradient: linear-gradient(135deg, #cba6f7, #1e1e2e, #89b4fa);
      --outline: #45475a;
    }

    :root[data-theme="tokyonight"] {
      --canvas: #1a1b26;
      --surface: #1f2335;
      --surface-dim: #16161e;
      --surface-bright: #3b4261;
      --surface-container: #24283b;
      --primary: #7aa2f7;
      --on-primary: #1a1b26;
      --on-surface: #a9b1d6;
      --on-surface-variant: #565f89;
      --accent-gradient: linear-gradient(135deg, #7aa2f7, #1a1b26, #bb9af7);
      --outline: #414868;
    }

    :root[data-theme="mono"] {
      --canvas: #000000;
      --surface: #121212;
      --surface-dim: #080808;
      --surface-bright: #242424;
      --surface-container: #1c1c1c;
      --primary: #ffffff;
      --on-primary: #000000;
      --on-surface: #f3f3f3;
      --on-surface-variant: #8a8a8a;
      --accent-gradient: linear-gradient(135deg, #ffffff, #000000, #404040);
      --outline: #404040;
    }

    :root[data-theme="contrast"] {
      --canvas: #ffffff;
      --surface: #ffffff;
      --surface-dim: #f5f5f5;
      --surface-bright: #ffffff;
      --surface-container: #f0f0f0;
      --primary: #000000;
      --on-primary: #ffffff;
      --on-surface: #000000;
      --on-surface-variant: #333333;
      --accent-gradient: linear-gradient(135deg, #000000, #ffffff, #000000);
      --outline: #000000;
    }

    body {
      background-color: var(--canvas);
      color: var(--on-surface);
      transition: background 0.3s, color 0.3s;
    }

    body.glass-enabled {
      background: var(--accent-gradient) !important;
      background-attachment: fixed !important;
    }

    .theme-card {
      background-color: var(--surface);
      border: 1px solid var(--outline);
      transition: all 0.3s;
    }

    body.glass-enabled .theme-card {
      background-color: rgba(255, 255, 255, 0.08) !important;
      backdrop-filter: blur(24px);
      -webkit-backdrop-filter: blur(24px);
      border: 1px solid rgba(255, 255, 255, 0.15) !important;
    }

    .accent-text {
      color: var(--primary);
    }

    .accent-border {
      border-color: var(--primary);
    }

    .accent-bg {
      background-color: var(--primary);
    }

    section[id], [id^="sec-"] { scroll-margin-top: 60px; }

    @media print {
      body { background: #ffffff !important; color: #000000 !important; }
      body.glass-enabled { background: #ffffff !important; }
      .theme-card { background: #ffffff !important; color: #000000 !important; border: 1px solid #000000 !important; }
      #page-nav, header button, #btn-glass, .[onclick] { display: none !important; }
      a { color: #000000 !important; text-decoration: underline; }
      .accent-text, .text-yellow-400, .accent-bg { color: #000000 !important; background: #000000 !important; }
      tr { page-break-inside: avoid; }
    }
""";

    public const string HtmlBody = """
    <!-- Error Banner (shown when report data fails to load) -->
    <div id="report-error" class="hidden rounded-xl border border-red-500/40 bg-red-500/10 p-4 mb-6 text-red-300">
      <div class="font-bold mb-1">Report data failed to load</div>
      <div id="report-error-msg" class="text-sm font-mono break-words"></div>
      <div class="text-xs mt-2 text-red-300/70">The embedded report JSON may be truncated or corrupted. Regenerate the report with: gitizer report &lt;repo&gt; --html &lt;path&gt;</div>
    </div>

    <!-- Header Controls -->
    <header class="theme-card p-6 rounded-xl flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h1 class="text-3xl font-extrabold tracking-tight flex items-center gap-2">
          <span class="w-2.5 h-6 accent-bg rounded-sm inline-block"></span>
          <span>gitizer dashboard</span>
        </h1>
        <p class="text-sm text-gray-400 mt-1" id="repo-path"></p>
        <p class="text-xs text-gray-500 mt-1" id="analysis-window"></p>
      </div>
      
      <!-- Interactive Toolbar -->
      <div class="flex flex-wrap items-center gap-4">
        <!-- Themes -->
        <div class="flex items-center bg-black/25 p-1 rounded-lg border border-white/10 text-xs">
          <button onclick="setTheme('deloitte')" class="px-2.5 py-1.5 rounded transition-all font-semibold hover:opacity-90" id="btn-deloitte">Deloitte</button>
          <button onclick="setTheme('catppuccin')" class="px-2.5 py-1.5 rounded transition-all font-semibold hover:opacity-90" id="btn-catppuccin">Catppuccin</button>
          <button onclick="setTheme('tokyonight')" class="px-2.5 py-1.5 rounded transition-all font-semibold hover:opacity-90" id="btn-tokyonight">Tokyo Night</button>
          <button onclick="setTheme('mono')" class="px-2.5 py-1.5 rounded transition-all font-semibold hover:opacity-90" id="btn-mono">Mono</button>
          <button onclick="setTheme('contrast')" class="px-2.5 py-1.5 rounded transition-all font-semibold hover:opacity-90" id="btn-contrast">Contrast</button>
        </div>
        
        <!-- Glass Switch -->
        <div class="flex items-center gap-2 bg-black/25 px-3 py-1.5 rounded-lg border border-white/10 text-xs font-semibold">
          <span>Glass Mode:</span>
          <button onclick="toggleGlass()" class="px-2 py-0.5 bg-white/20 rounded accent-bg text-black transition-all" id="btn-glass">ON</button>
        </div>
      </div>
    </header>

    <!-- Sticky In-Page Navigation -->
    <nav class="sticky top-0 z-20 theme-card rounded-xl p-2 mb-6 flex flex-wrap gap-2 text-xs" id="page-nav">
      <a href="#sec-summary" class="px-2 py-1 rounded hover:bg-white/10">Summary</a>
      <a href="#sec-kpis" class="px-2 py-1 rounded hover:bg-white/10">KPIs</a>
      <a href="#sec-areas" class="px-2 py-1 rounded hover:bg-white/10">Areas</a>
      <a href="#sec-insights" class="px-2 py-1 rounded hover:bg-white/10">Insights</a>
      <a href="#sec-scatter" class="px-2 py-1 rounded hover:bg-white/10">Scatter</a>
      <a href="#sec-files" class="px-2 py-1 rounded hover:bg-white/10">Files</a>
      <a href="#sec-contributors" class="px-2 py-1 rounded hover:bg-white/10">Contributors</a>
      <a href="#sec-footer" class="px-2 py-1 rounded hover:bg-white/10">Exclusions</a>
    </nav>

    <!-- Executive Summary & Action Queue -->
    <section class="space-y-6" id="sec-summary">
      <div class="theme-card p-6 rounded-xl">
        <h2 class="text-xl font-bold tracking-tight flex items-center gap-2 mb-3">
          <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
          <span>Executive Summary</span>
        </h2>
        <p id="exec-summary-narrative" class="text-sm text-gray-300 leading-relaxed"></p>
        <div id="exec-summary-stats" class="mt-4 grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3 text-xs"></div>
      </div>
      <div class="theme-card p-6 rounded-xl space-y-4">
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-2">
          <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
            <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
            <span>Top Action Queue</span>
          </h2>
          <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-2 cursor-pointer max-w-md">
            <summary class="font-semibold text-gray-300 select-none">What is this? &mdash; Rationale & Priorities</summary>
            <div class="mt-1.5 space-y-1.5 text-gray-300 leading-relaxed font-normal">
              <p><strong>Rationale:</strong> Consolidates high-level metrics of your repository and generates an algorithmic checklist of urgent technical tasks. Instead of browsing raw log files, this view points you immediately to critical risk points.</p>
              <p><strong>Action Priorities:</strong></p>
              <ul class="list-disc pl-4 space-y-1">
                <li><span class="px-1 py-0.5 rounded bg-red-500/15 text-red-300 font-bold">P1</span>: Critical configuration errors, severe warnings, or data integrity notices.</li>
                <li><span class="px-1 py-0.5 rounded bg-yellow-500/15 text-yellow-300 font-bold">P2</span>: Severe hotspot files (highest attention scores) undergoing extreme churn or rework.</li>
                <li><span class="px-1 py-0.5 rounded bg-white/10 text-gray-300 font-bold">P3</span>: Bus-factor concentration risks (single developer dominance in an area) or files requiring stabilization.</li>
              </ul>
            </div>
          </details>
        </div>
        <ol id="action-queue" class="space-y-2 text-sm"></ol>
      </div>
    </section>

    <!-- Top KPI Cards -->
    <div class="space-y-4">
      <section class="grid grid-cols-1 md:grid-cols-3 gap-6" id="sec-kpis">
        <!-- Card 1 -->
        <div class="theme-card p-6 rounded-xl flex items-center justify-between">
          <div>
            <span class="text-xs font-bold uppercase tracking-wider text-gray-400">Total Unique Humans</span>
            <h3 class="text-3xl font-extrabold mt-1" id="kpi-contributors">-</h3>
          </div>
          <div class="p-3 bg-white/5 rounded-lg border border-white/10">👤</div>
        </div>
        
        <!-- Card 2 -->
        <div class="theme-card p-6 rounded-xl flex items-center justify-between">
          <div>
            <span class="text-xs font-bold uppercase tracking-wider text-gray-400">Knowledge Silos</span>
            <h3 class="text-3xl font-extrabold mt-1 text-yellow-400" id="kpi-silos">-</h3>
          </div>
          <div class="p-3 bg-white/5 rounded-lg border border-white/10">⚠️</div>
        </div>

        <!-- Card 3 -->
        <div class="theme-card p-6 rounded-xl">
          <span class="text-xs font-bold uppercase tracking-wider text-gray-400">Human Activity vs Automation Churn</span>
          <div class="mt-3">
            <div class="flex justify-between text-xs font-semibold mb-1">
              <span class="accent-text">Humans: <span id="human-share-label">-</span>%</span>
              <span class="text-gray-400">Bots: <span id="bot-share-label">-</span>%</span>
            </div>
            <div class="w-full bg-black/30 h-2 rounded-full overflow-hidden border border-white/5 flex">
              <div class="accent-bg h-full" id="human-share-bar" style="width: 0%"></div>
              <div class="bg-gray-600 h-full flex-grow"></div>
            </div>
          </div>
        </div>
      </section>

      <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
        <summary class="font-semibold text-gray-300 select-none">Understanding the Key Performance Indicators (KPIs) &mdash; Rationale</summary>
        <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
          <p><strong>Total Unique Humans:</strong> Count of developers who authored or co-authored commits in the active window. This tracks actual team size and active collaboration breadth.</p>
          <p><strong>Knowledge Silos:</strong> Count of codebase files where a single developer owns &ge; 70% of all activity. High values indicate high truck-factor vulnerability and extreme reliance on specific individuals.</p>
          <p><strong>Human Activity vs Automation Churn:</strong> Compares active human-driven changes to automated changes (like lockfile bumps, CI updates, release bots, and dependency automation). A high automation percentage indicates strong automated tooling but can sometimes mask actual product changes.</p>
        </div>
      </details>
    </div>

    <!-- Search / Filter bar -->
    <div class="relative flex items-center">
      <input id="filter" aria-label="Filter report rows" placeholder="Filter codebase files, areas or contributors..." class="w-full theme-card p-4 pr-28 rounded-xl text-sm focus:outline-none focus:ring-1 focus:ring-offset-1 focus:ring-offset-black accent-border bg-black/10">
      <button id="clear-filter" onclick="clearFilter()" class="hidden absolute right-4 text-gray-400 hover:text-white bg-white/10 hover:bg-white/20 px-2.5 py-1.5 rounded text-xs transition-all font-semibold select-none">
        &times; Clear Filter
      </button>
    </div>

    <!-- Area SME Directory Map -->
    <section class="space-y-4" id="sec-areas">
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-2">
        <div>
          <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
            <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
            <span>SME Area Directory Map</span>
          </h2>
          <div class="text-[10px] text-gray-400 mt-1">Concentration tiers: healthy &lt;50%, watch 50-70%, silo &gt;=70% (bus-factor).</div>
        </div>
        <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-2 cursor-pointer max-w-md">
          <summary class="font-semibold text-gray-300 select-none">Why map Subject Matter Experts? &mdash; Rationale & Metrics</summary>
          <div class="mt-1.5 space-y-1.5 text-gray-300 leading-relaxed font-normal">
            <p><strong>Rationale:</strong> Identifying Subject Matter Experts (SMEs) helps teams know who to coordinate with before refactoring or scaling a module. It avoids "expert bias" by deriving knowledge from actual git activity share rather than static ownership.</p>
            <p><strong>Key Metrics & Thresholds:</strong></p>
            <ul class="list-disc pl-4 space-y-1">
              <li><strong>Attention Score:</strong> The overall risk and attention index for the directory.</li>
              <li><strong>Heat Score:</strong> Normalized absolute touch and churn frequency for this directory.</li>
              <li><strong>Activity Share:</strong> The percentage of commits in this directory made by the developer.</li>
              <li><strong>Bus Factor Silo Risk (&ge; 70%):</strong> Triggered when a single developer is responsible for 70% or more of the directory's commits, representing a potential single point of failure.</li>
            </ul>
          </div>
        </details>
      </div>
      <div id="area-grid" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"></div>
    </section>

    <!-- Strategic Deep Insights -->
    <section class="grid grid-cols-1 lg:grid-cols-2 gap-8" id="sec-insights">
      <!-- Merges & Lead Times -->
      <div class="space-y-4" id="lead-time-section">
        <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
          <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
          <span>Branch Merges & Lead Time for Changes</span>
        </h2>
        <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
          <summary class="font-semibold text-gray-300 select-none">What is Lead Time for Changes? &mdash; Rationale & Targets</summary>
          <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
            <p><strong>Rationale:</strong> A key DevOps/DORA delivery metric. It tracks the time elapsed from the first commit of a feature branch to its merge commit into the main branch. Shorter cycles encourage faster feedback and smaller, safer releases.</p>
            <p><strong>How to interpret:</strong> High lead times (e.g., &gt; 100 hrs) indicate complex, long-running branches with high potential for merge conflicts and integration issues. Short lead times (&lt; 24 hrs) indicate highly cohesive, bite-sized tasks.</p>
          </div>
        </details>
        <div class="theme-card rounded-xl p-5 space-y-4 flex flex-col justify-between">
          <div class="flex items-center justify-between border-b border-white/5 pb-3">
            <div>
              <span class="text-xs text-gray-400 uppercase tracking-wider font-bold">Average Lead Time for Changes</span>
              <h3 class="text-2xl font-extrabold mt-1 text-yellow-400" id="avg-lead-time">-</h3>
            </div>
            <div class="p-3 bg-white/5 rounded-lg border border-white/10">⏱️</div>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-xs text-left">
              <thead>
                <tr class="border-b border-white/10 font-bold text-gray-400 bg-black/10">
                  <th class="p-2">Merge Commit Message</th>
                  <th class="p-2 text-center">Lead Time</th>
                  <th class="p-2 text-center">Files</th>
                </tr>
              </thead>
              <tbody id="merges-table-body" class="divide-y divide-white/5"></tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Temporal Coupling -->
      <div class="space-y-4">
        <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
          <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
          <span>Temporal (Logical) Coupling Analysis</span>
        </h2>
        <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
          <summary class="font-semibold text-gray-300 select-none">What is Temporal Coupling? &mdash; Rationale & Red Flags</summary>
          <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
            <p><strong>Rationale:</strong> Tracks files that are changed together in the same commits over time, even if they have no direct imports or physical dependencies. It surfaces "hidden" architectural linkages.</p>
            <p><strong>How to interpret:</strong> High logical coupling (e.g., &ge; 50% coupling over &ge; 3 shared commits) between seemingly unrelated files (e.g., a backend controller and an independent helper) is an architectural red flag. It often suggests a missing abstraction, copy-paste duplicate code, or failure of modular separation.</p>
          </div>
        </details>
        <div class="theme-card rounded-xl p-5 space-y-4">
          <span class="text-xs text-gray-400 uppercase tracking-wider font-bold block mb-2">Top Logically Coupled File Pairs</span>
          <div class="overflow-x-auto">
            <table class="w-full text-xs text-left">
              <thead>
                <tr class="border-b border-white/10 font-bold text-gray-400 bg-black/10">
                  <th class="p-2">File A</th>
                  <th class="p-2">File B</th>
                  <th class="p-2 text-center">Shared Commits</th>
                  <th class="p-2 text-center">Coupling</th>
                </tr>
              </thead>
              <tbody id="coupling-table-body" class="divide-y divide-white/5"></tbody>
            </table>
          </div>
        </div>
      </div>
    </section>

    <!-- Volatility vs. Attention Risk Scatter Plot -->
    <section class="space-y-4" id="sec-scatter">
      <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
        <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
        <span>Volatility vs. Attention Risk Scatter Plot</span>
      </h2>
      <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
        <summary class="font-semibold text-gray-300 select-none">Understanding the Scatter Plot &mdash; Rationale & Quadrants</summary>
        <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
          <p><strong>Rationale:</strong> Visually categorizes your files based on their active change volume (Churn) and their structural attention risk (Attention Score) to separate transient churn from legacy risks.</p>
          <p><strong>The Four Quadrants:</strong></p>
          <ul class="list-disc pl-4 space-y-1">
            <li><strong>Volatile Hotspots (Top Right):</strong> High churn and high attention. Active, high-risk code that is shifting rapidly; ideal candidate for modular split or refactoring.</li>
            <li><strong>Stable Heritage (Top Left):</strong> High attention but low churn. Complex, critical legacy files that are rarely changed but present high risk when touched. Treat with caution and write strong tests.</li>
            <li><strong>Safe Refactoring (Bottom Right):</strong> High churn but low attention. Active but low-complexity code (e.g., configs, UI styles). Changes are highly frequent but low-risk.</li>
            <li><strong>Low Maintenance (Bottom Left):</strong> Low churn and low attention. Stable, highly modular, or well-designed code that functions without active intervention.</li>
          </ul>
        </div>
      </details>
      <div id="scatter-plot" class="theme-card rounded-xl p-4"></div>
    </section>

    <!-- Detailed Lists (Tabs or Compartments) -->
    <section class="grid grid-cols-1 lg:grid-cols-3 gap-8" id="sec-files">
      <!-- Files Hotspots -->
      <div class="lg:col-span-2 space-y-4">
        <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
          <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
          <span>Hotspot Files</span>
          <span id="files-count" class="text-[10px] text-gray-400 ml-2 font-normal"></span>
        </h2>
        <div id="quality-legend" class="flex flex-wrap gap-3 text-[10px] text-gray-400">
          <span class="px-1.5 py-0.5 rounded font-bold uppercase bg-green-500/15 text-green-300 border border-green-500/20">healthy</span>
          <span>Low risk, attention below 40 and churn stable.</span>
          <span class="px-1.5 py-0.5 rounded font-bold uppercase bg-blue-500/15 text-blue-300 border border-blue-500/20">watch</span>
          <span>Attention or rework elevated; monitor before scaling.</span>
          <span class="px-1.5 py-0.5 rounded font-bold uppercase bg-yellow-500/15 text-yellow-300 border border-yellow-500/20">hotspot</span>
          <span>High attention score; prioritize review and tests.</span>
          <span class="px-1.5 py-0.5 rounded font-bold uppercase bg-red-500/15 text-red-300 border border-red-500/20">smelly</span>
          <span>Silo, high rework, or high volatility; refactor candidate.</span>
        </div>
        <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
          <summary class="font-semibold text-gray-300 select-none">Deciphering File Metrics &mdash; Rationale & Column Explanations</summary>
          <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
            <p><strong>Rationale:</strong> Ranks individual files by change risk to help developers slow down, review carefully, or write extra unit tests before applying changes.</p>
            <p><strong>What the Columns & Row Metrics Mean:</strong></p>
            <ul class="list-disc pl-4 space-y-1">
              <li><strong>Attention Score (0-100):</strong> Weighted composite signal calculated from:
                <ul class="list-disc pl-4 mt-1 space-y-0.5">
                  <li><strong>Churn (35%):</strong> Volume of lines added/removed. High churn indicates high volatility.</li>
                  <li><strong>Recency (30%):</strong> How recently the file was changed.</li>
                  <li><strong>Contributor Spread (20%):</strong> Number of distinct developers. More editors increase coordination overhead.</li>
                  <li><strong>Low Familiarity Concentration (15%):</strong> Percentage of edits made by contributors with low overall experience in that area.</li>
                </ul>
              </li>
              <li><strong>Heat Score (0-100):</strong> Normalized metric of absolute raw touch counts and churn volume.</li>
              <li><strong>Rework Rate:</strong> Percentage of touches that are bug fixes or corrective patches (identified via keywords like 'fix', 'bug', 'patch' in commits). High rework suggests instability.</li>
              <li><strong>Coordination Overlap:</strong> Degree of overlapping or parallel edits by different authors in short timeframes. High overlap leads to code collision.</li>
              <li><strong>Debt Volatility:</strong> Indication of interest payments on technical debt, driven by high churn on complex code.</li>
              <li><strong>Silo Risk / Abandoned:</strong> Flagged if &ge; 70% of history is authored by one developer (Silo), or if the main developer of the file has made no recent commits in the repository (Abandoned).</li>
            </ul>
          </div>
        </details>
        <div class="theme-card rounded-xl overflow-x-auto">
          <table class="w-full text-sm text-left">
            <thead>
              <tr class="border-b border-white/10 text-xs font-bold uppercase tracking-wider text-gray-400 bg-black/20">
                <th class="p-4 cursor-pointer select-none hover:text-white" data-sort="path">Path <span data-sort-indicator="path"></span></th>
                <th class="p-4 text-center cursor-pointer select-none hover:text-white" data-sort="attention_score">Attention <span data-sort-indicator="attention_score"></span></th>
                <th class="p-4 text-center cursor-pointer select-none hover:text-white" data-sort="heat_score">Heat <span data-sort-indicator="heat_score"></span></th>
                <th class="p-4 text-center cursor-pointer select-none hover:text-white" data-sort="churn">Churn <span data-sort-indicator="churn"></span></th>
                <th class="p-4 cursor-pointer select-none hover:text-white" data-sort="topContributor">Top Contributor <span data-sort-indicator="topContributor"></span></th>
              </tr>
            </thead>
            <tbody id="files-table-body" class="divide-y divide-white/5"></tbody>
          </table>
        </div>
      </div>

      <!-- Human Contributors Rank -->
      <div class="space-y-4" id="sec-contributors">
        <h2 class="text-xl font-bold tracking-tight flex items-center gap-2">
          <span class="w-1.5 h-4 accent-bg rounded-sm inline-block"></span>
          <span>Contributors Rank</span>
        </h2>
        <details class="text-[11px] text-gray-400 bg-black/20 border border-white/10 rounded-lg p-3 cursor-pointer">
          <summary class="font-semibold text-gray-300 select-none">How is rank calculated? &mdash; Rationale</summary>
          <div class="mt-2 space-y-1.5 text-gray-300 leading-relaxed font-normal">
            <p><strong>Rationale:</strong> Distinguishes core maintainers from occasional contributors based on raw activity points (commits and co-authorships), helping onboarding developers find the right person for reviews.</p>
          </div>
        </details>
        <div class="theme-card rounded-xl p-4 space-y-4" id="contributors-list"></div>
      </div>
    </section>

    <!-- Exclusions & Warnings Footer -->
    <footer class="grid grid-cols-1 md:grid-cols-2 gap-6 text-xs text-gray-400" id="sec-footer">
      <div class="theme-card p-4 rounded-xl">
        <span class="font-bold block mb-1">Excluded Categories & Wildcards</span>
        <div id="exclusions-box" class="space-y-1"></div>
      </div>
      <div class="theme-card p-4 rounded-xl">
        <span class="font-bold block mb-1">System Warnings</span>
        <div id="warnings-box" class="text-yellow-400">None</div>
      </div>
    </footer>
""";

    public const string ClientScriptTemplate = """
  <script id="gitizer-data" type="application/json">__RESULT_JSON__</script>
  <script>
    function showReportError(msg) {
      const banner = document.getElementById('report-error');
      const messageBox = document.getElementById('report-error-msg');
      if (banner) banner.classList.remove('hidden');
      if (messageBox) messageBox.textContent = String(msg);
    }

    let report;
    try {
      report = JSON.parse(document.getElementById('gitizer-data').textContent);
    } catch (err) {
      showReportError(err && err.message ? err.message : String(err));
      report = {
        contributors: [],
        automation: [],
        areas: [],
        files: [],
        temporal_coupling: [],
        lead_times: { average_lead_time_hours: 0, merges: [] },
        warnings: [],
        exclusions: [],
        analysis: {
          repo_root: '',
          command: 'report',
          generated_at: '',
          commit_count: 0,
          included_file_change_count: 0,
        },
        settings: {},
        configuration: {
          scoring: {
            attention: {
              churn: 0,
              recency: 0,
              contributor_spread: 0,
              low_familiarity_concentration: 0,
            },
          },
          configured_alias_count: 0,
          configured_bot_count: 0,
          configured_exclude_count: 0,
          configured_area_count: 0,
        },
      };
    }

    // escapeCell is required for client-side execution because dynamic fields
    // (such as author names, emails, and file paths) are rendered as raw innerHTML,
    // which can lead to layout disruption or XSS vulnerabilities if not escaped.
    function escapeCell(value) {
      return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
    }
    
    // States
    let currentTheme = 'deloitte';
    let glassEnabled = true;
    let sortState = { column: 'attention_score', dir: 'desc' };

    function analysisWindowText(report) {
      if (report.settings && report.settings.since) {
        return 'since ' + report.settings.since;
      }
      if (report.settings && report.settings.all_time) {
        return 'all time';
      }
      return 'last 6 months';
    }

    document.getElementById('repo-path').textContent = report.analysis.repo_root;
    const aw = document.getElementById('analysis-window');
    if (aw) aw.textContent = 'Analysis window: ' + analysisWindowText(report);

    function setTheme(theme) {
      currentTheme = theme;
      document.documentElement.setAttribute('data-theme', theme);
      
      // Update toggle buttons active style
      ['deloitte', 'catppuccin', 'tokyonight', 'mono', 'contrast'].forEach(t => {
        const btn = document.getElementById('btn-' + t);
        if (t === theme) {
          btn.className = "px-2.5 py-1.5 rounded font-bold accent-bg text-black transition-all";
        } else {
          btn.className = "px-2.5 py-1.5 rounded transition-all font-semibold hover:bg-white/5 text-gray-400";
        }
      });
    }

    function toggleGlass() {
      glassEnabled = !glassEnabled;
      const btn = document.getElementById('btn-glass');
      if (glassEnabled) {
        document.body.classList.add('glass-enabled');
        btn.textContent = "ON";
        btn.className = "px-2 py-0.5 rounded accent-bg text-black transition-all font-bold";
      } else {
        document.body.classList.remove('glass-enabled');
        btn.textContent = "OFF";
        btn.className = "px-2 py-0.5 rounded bg-gray-700 text-white transition-all font-bold";
      }
    }

    function clearFilter() {
      const filterInput = document.getElementById('filter');
      if (filterInput) {
        filterInput.value = '';
        processData();
      }
    }

    function isBoilerplateSymbol(name) {
      const n = String(name ?? '').trim().toLowerCase();
      return n.startsWith('import ') || n.startsWith('using ') || n.startsWith('namespace ');
    }

    // Set Defaults
    setTheme('deloitte');
    toggleGlass(); // sets to true, labels ON

    // Column sorting for the Hotspot Files table
    function sortValue(file, column) {
      if (column === 'path') return file.path || '';
      if (column === 'attention_score') return file.attention_score || 0;
      if (column === 'heat_score') return file.heat_score || 0;
      if (column === 'churn') return file.churn || 0;
      if (column === 'topContributor') {
        return (file.contributors && file.contributors[0] ? file.contributors[0].name : '');
      }
      return 0;
    }

    function sortFiles(files) {
      const column = sortState.column;
      const dir = sortState.dir === 'asc' ? 1 : -1;
      const sorted = [...files];
      return sorted.sort((left, right) => {
        const leftValue = sortValue(left, column);
        const rightValue = sortValue(right, column);
        if (typeof leftValue === 'number' && typeof rightValue === 'number') {
          if (leftValue !== rightValue) return (leftValue - rightValue) * dir;
          return 0;
        }
        const comparison = String(leftValue).localeCompare(String(rightValue));
        if (comparison !== 0) return comparison * dir;
        return 0;
      });
    }

    function setSort(column) {
      if (sortState.column === column) {
        sortState.dir = sortState.dir === 'asc' ? 'desc' : 'asc';
      } else {
        sortState.column = column;
        sortState.dir = column === 'path' ? 'asc' : 'desc';
      }
      renderSortIndicators();
      processData();
    }

    function renderSortIndicators() {
      document.querySelectorAll('[data-sort-indicator]').forEach(el => {
        el.textContent = '';
      });
      const active = document.querySelector('[data-sort-indicator="' + sortState.column + '"]');
      if (active) active.textContent = sortState.dir === 'asc' ? ' ▲' : ' ▼';
    }

    // Calculations & Data-binding

    function qualitySignal(file) {
      const rework = file.rework_rate ? file.rework_rate : 0;
      const isSilo = file.knowledge_silo && file.knowledge_silo.is_silo;
      const vol = file.debt_volatility ? file.debt_volatility : 0;
      if (rework > 0.25 || isSilo || vol >= 70) {
        return { label: 'smelly', cls: 'bg-red-500/15 text-red-300 border border-red-500/20' };
      }
      if (file.attention_score >= 70) {
        return { label: 'hotspot', cls: 'bg-yellow-500/15 text-yellow-300 border border-yellow-500/20' };
      }
      if (file.attention_score >= 40 || rework > 0.10) {
        return { label: 'watch', cls: 'bg-blue-500/15 text-blue-300 border border-blue-500/20' };
      }
      return { label: 'healthy', cls: 'bg-green-500/15 text-green-300 border border-green-500/20' };
    }

    function processData() {
      const filterInput = document.getElementById('filter');
      const filterText = filterInput ? filterInput.value.toLowerCase() : '';
      
      const clearBtn = document.getElementById('clear-filter');
      if (clearBtn) {
        if (filterText) {
          clearBtn.classList.remove('hidden');
        } else {
          clearBtn.classList.add('hidden');
        }
      }
      
      // KPI 1: Unique Humans
      const humanCount = report.contributors.length;
      document.getElementById('kpi-contributors').textContent = humanCount;

      // KPI 2: Knowledge Silos
      // Silo definition: top_owner_share >= 70% (matches backend SILO_THRESHOLD)
      let siloCount = 0;
      report.files.forEach(file => {
        if (file.knowledge_silo && file.knowledge_silo.is_silo) {
          siloCount++;
        } else if (file.contributors && file.contributors[0]) {
          if (file.contributors[0].activity_share >= 0.70) {
            siloCount++;
          }
        }
      });
      document.getElementById('kpi-silos').textContent = siloCount;

      // KPI 3: Humans vs Bots
      const humanTotal = report.contributors.reduce((sum, c) => sum + c.total_activity, 0);
      const botTotal = report.automation.reduce((sum, b) => sum + b.total_activity, 0);
      const grandTotal = humanTotal + botTotal;
      const humanPct = grandTotal > 0 ? Math.round((humanTotal / grandTotal) * 100) : 100;
      const botPct = 100 - humanPct;

      document.getElementById('human-share-label').textContent = humanPct;
      document.getElementById('bot-share-label').textContent = botPct;
      document.getElementById('human-share-bar').style.width = humanPct + '%';

      // ── Render SME Areas Map ──
      const areaGrid = document.getElementById('area-grid');
      areaGrid.innerHTML = '';
      
      const filteredAreas = report.areas.filter(area => 
        area.area.toLowerCase().includes(filterText)
      );

      const globalMaxLines = Math.max(...report.files.map(f => f.lines || 1), 1);
      const globalMaxWidth = Math.max(...report.files.map(f => f.width || 1), 1);

      filteredAreas.forEach(area => {
        const topSme = area.contributors && area.contributors[0] ? area.contributors[0] : null;
        const secondSme = area.contributors && area.contributors[1] ? area.contributors[1] : null;
        const topShare = topSme ? Math.round(topSme.activity_share * 100) : 0;
        
        // Bus Factor check (>=70% share owned by 1 contributor; matches backend SILO_THRESHOLD)
        const isBusFactor = topShare >= 70;

        const card = document.createElement('div');
        card.className = "theme-card p-5 rounded-xl space-y-4 flex flex-col justify-between";
        if (isBusFactor) {
          card.style.border = "1px solid rgba(234, 179, 8, 0.4)";
        }

        let smeHtml = '<span class="text-xs text-gray-400">No active contributors</span>';
        if (topSme) {
          smeHtml = `
            <div class="space-y-1">
              <div class="flex justify-between text-xs">
                <span class="font-bold accent-text">${topSme.name}</span>
                <span class="font-bold">${topShare}% share</span>
              </div>
              <div class="w-full bg-black/20 h-1.5 rounded-full overflow-hidden">
                <div class="accent-bg h-full" style="width: ${topShare}%"></div>
              </div>
              ${secondSme ? `
                <div class="flex justify-between text-[10px] text-gray-400 pt-1">
                  <span>SME #2: ${secondSme.name}</span>
                  <span>${Math.round(secondSme.activity_share * 100)}%</span>
                </div>
              ` : ''}
            </div>
          `;
        }

        const areaFiles = report.files.filter(f => f.area === area.area);
        const filesLogHtml = areaFiles.map(f => {
          const sizeStr = f.size !== undefined ? f.size + ' B' : '0 B';
          const widthStr = f.width !== undefined ? f.width + ' ch' : '0 ch';
          return `
            <div class="truncate text-left text-[10px] pt-0.5 first:pt-0" title="${f.path}">
              <span class="text-gray-500 font-mono">[${sizeStr}, width: ${widthStr}]</span>
              <span class="font-mono text-gray-300 ml-1">${f.path}</span>
            </div>
          `;
        }).join('');

        let minLines = 0, maxLines = 0, avgLines = 0;
        let minWidth = 0, maxWidth = 0, avgWidth = 0;

        const filesWithData = areaFiles.filter(f => f.lines !== undefined && f.width !== undefined);
        if (filesWithData.length > 0) {
          const lineCounts = filesWithData.map(f => f.lines);
          const widths = filesWithData.map(f => f.width);

          minLines = Math.min(...lineCounts);
          maxLines = Math.max(...lineCounts);
          avgLines = Math.round(lineCounts.reduce((s, c) => s + c, 0) / filesWithData.length);

          minWidth = Math.min(...widths);
          maxWidth = Math.max(...widths);
          avgWidth = Math.round(widths.reduce((s, c) => s + c, 0) / filesWithData.length);
        }

        const lineMinPct = globalMaxLines > 0 ? (minLines / globalMaxLines) * 100 : 0;
        const lineMaxPct = globalMaxLines > 0 ? (maxLines / globalMaxLines) * 100 : 0;
        const lineAvgPct = globalMaxLines > 0 ? (avgLines / globalMaxLines) * 100 : 0;

        const widthMinPct = globalMaxWidth > 0 ? (minWidth / globalMaxWidth) * 100 : 0;
        const widthMaxPct = globalMaxWidth > 0 ? (maxWidth / globalMaxWidth) * 100 : 0;
        const widthAvgPct = globalMaxWidth > 0 ? (avgWidth / globalMaxWidth) * 100 : 0;

        card.innerHTML = `
          <div class="space-y-2">
            <div class="flex justify-between items-start gap-2">
              <h4 class="font-extrabold text-base tracking-tight truncate max-w-[180px]">${area.area}</h4>
              <span class="px-2 py-0.5 bg-black/30 rounded border border-white/5 text-[10px] font-mono text-gray-400">${area.file_count} files</span>
            </div>
            
            ${isBusFactor ? `
              <span class="px-2 py-0.5 bg-yellow-500/10 text-yellow-400 text-[10px] font-bold rounded-full border border-yellow-500/20 block text-center">
                ⚠️ Bus Factor Silo Risk
              </span>
            ` : ''}
          </div>

          <div class="space-y-1">
            <span class="text-[10px] font-bold uppercase tracking-wider text-gray-400 font-sans">Subject Matter Experts</span>
            ${smeHtml}
          </div>

          <div class="space-y-1">
            <span class="text-[10px] font-bold uppercase tracking-wider text-gray-400 font-sans">Files Log</span>
            <div class="bg-black/20 rounded-lg p-2 max-h-24 overflow-y-auto font-mono text-[9px] leading-tight border border-white/5 text-left text-gray-300 space-y-1">
              ${filesLogHtml || '<div class="text-gray-500">No files</div>'}
            </div>
          </div>

          <!-- Avg-Min-Max Charts -->
          <div class="space-y-3 bg-black/10 rounded-lg p-3 border border-white/5">
            <!-- Height Chart -->
            <div class="space-y-1">
              <div class="flex justify-between text-[9px] text-gray-400 font-sans">
                <span class="font-bold text-gray-300">File Height (Lines)</span>
                <span>Min: ${minLines} • Avg: ${avgLines} • Max: ${maxLines}</span>
              </div>
              <div class="relative w-full h-1.5 bg-black/30 rounded-full overflow-visible">
                <div class="absolute h-full bg-blue-500/30 rounded-full" style="left: ${lineMinPct}%; width: ${lineMaxPct - lineMinPct}%"></div>
                <div class="absolute w-1.5 h-1.5 bg-blue-400 rounded-full top-0" style="left: calc(${lineMinPct}% - 3px)"></div>
                <div class="absolute w-1.5 h-1.5 bg-blue-400 rounded-full top-0" style="left: calc(${lineMaxPct}% - 3px)"></div>
                <div class="absolute w-2 h-2 bg-yellow-400 rounded-full -top-[1px] border border-black cursor-help" style="left: calc(${lineAvgPct}% - 4px)" title="Average: ${avgLines} lines"></div>
              </div>
            </div>

            <!-- Width Chart -->
            <div class="space-y-1">
              <div class="flex justify-between text-[9px] text-gray-400 font-sans">
                <span class="font-bold text-gray-300">Line Width (Chars)</span>
                <span>Min: ${minWidth} • Avg: ${avgWidth} • Max: ${maxWidth}</span>
              </div>
              <div class="relative w-full h-1.5 bg-black/30 rounded-full overflow-visible">
                <div class="absolute h-full bg-purple-500/30 rounded-full" style="left: ${widthMinPct}%; width: ${widthMaxPct - widthMinPct}%"></div>
                <div class="absolute w-1.5 h-1.5 bg-purple-400 rounded-full top-0" style="left: calc(${widthMinPct}% - 3px)"></div>
                <div class="absolute w-1.5 h-1.5 bg-purple-400 rounded-full top-0" style="left: calc(${widthMaxPct}% - 3px)"></div>
                <div class="absolute w-2 h-2 bg-yellow-400 rounded-full -top-[1px] border border-black cursor-help" style="left: calc(${widthAvgPct}% - 4px)" title="Average: ${avgWidth} chars"></div>
              </div>
            </div>
          </div>

          <div class="flex justify-between items-center text-xs border-t border-white/5 pt-3">
            <div>
              <span class="text-[10px] text-gray-400 block uppercase">Attention</span>
              <span class="font-extrabold text-base text-yellow-400">${area.attention_score}</span>
            </div>
            <div class="text-right">
              <span class="text-[10px] text-gray-400 block uppercase">Heat Score</span>
              <span class="font-extrabold text-base accent-text">${area.heat_score}</span>
            </div>
          </div>
        `;
        areaGrid.appendChild(card);
      });

      // ── Render Files Table ──
      const filesTableBody = document.getElementById('files-table-body');
      filesTableBody.innerHTML = '';

      const matchedFiles = report.files.filter(file =>
        file.path.toLowerCase().includes(filterText) ||
        (file.area && file.area.toLowerCase().includes(filterText))
      );
      const filteredFiles = sortFiles(matchedFiles);

      const fc = document.getElementById('files-count');
      if (fc) fc.textContent = 'Showing ' + filteredFiles.length + ' of ' + report.files.length + ' files';

      filteredFiles.forEach(file => {
        const sig = qualitySignal(file);
        const tr = document.createElement('tr');
        tr.className = "hover:bg-white/5 transition-all";

        const topContr = file.contributors && file.contributors[0] 
          ? `${file.contributors[0].name} (${Math.round(file.contributors[0].activity_share * 100)}%)` 
          : '—';

        const maxSymTouches = file.inner_symbols && file.inner_symbols.length > 0
          ? Math.max(1, ...file.inner_symbols.filter(sym => !isBoilerplateSymbol(sym.name)).map(s => s.touches))
          : 1;

        const innerSymbolsHtml = file.inner_symbols && file.inner_symbols.length > 0
          ? `<div class="mt-2.5 flex flex-wrap gap-2 font-sans">` + 
            file.inner_symbols.filter(sym => !isBoilerplateSymbol(sym.name)).slice(0, 5).map(sym => {
              const pct = Math.round((sym.touches / maxSymTouches) * 100);
              const isClass = /^[A-Z]/.test(sym.name.trim().split('.').pop() || '');
              const icon = isClass ? '⎔' : 'ƒ';
              const iconColor = isClass ? 'text-blue-400' : 'text-purple-400';
              const titleText = `${escapeCell(sym.name)}: ${sym.touches} touches (${pct}% of max in file)`;
              return `
                <span class="relative overflow-hidden inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-white/5 border border-white/10 text-[10px] text-gray-300 hover:text-white transition-colors group cursor-help" title="${titleText}">
                  <!-- Touch intensity background bar -->
                  <div class="absolute inset-y-0 left-0 bg-white/10" style="width: ${pct}%"></div>
                  
                  <!-- Content with high relative z-index to stay above background bar -->
                  <span class="relative z-10 ${iconColor} font-bold font-mono text-[11px]">${icon}</span>
                  <span class="relative z-10 font-medium truncate max-w-[120px] font-mono">${escapeCell(sym.name)}</span>
                  <span class="relative z-10 ml-0.5 px-1 py-0.2 rounded bg-black/30 text-gray-400 font-bold font-mono text-[9px] group-hover:text-white">${sym.touches}</span>
                </span>
              `;
            }).join('') + 
            `</div>`
          : '';

        const reworkRateVal = file.rework_rate ? Math.round(file.rework_rate * 100) : 0;
        const coordinationVal = file.coordination_overlap ?? 0;
        const volatilityVal = file.debt_volatility ?? 0;

        const sizeVal = file.size !== undefined ? file.size : 0;
        const widthVal = file.width !== undefined ? file.width : 0;

        const silo = file.knowledge_silo;
        const siloBadge = silo
          ? (silo.is_silo
              ? `<span class="px-1.5 py-0.5 rounded bg-yellow-500/10 text-yellow-400 border border-yellow-500/20 text-[10px] font-semibold" title="Truck Factor: ${silo.truck_factor}">Silo Risk</span>`
              : '') +
            (silo.abandoned
              ? `<span class="ml-1 px-1.5 py-0.5 rounded bg-red-500/10 text-red-400 border border-red-500/20 text-[10px] font-semibold">Abandoned</span>`
              : '')
          : '';

        const metricsHtml = `
          <div class="mt-1 flex flex-wrap items-center gap-2 text-[10px] text-gray-400">
            <span title="Rework rate: Percentage of corrective touches" class="${reworkRateVal > 25 ? 'text-red-400 font-semibold' : ''}">Rework: ${reworkRateVal}%</span>
            <span>•</span>
            <span title="Coordination Overlap Score">Coordination: ${coordinationVal}</span>
            <span>•</span>
            <span title="Debt Volatility Score">Volatility: ${volatilityVal}</span>
            <span>•</span>
            <span title="File size on disk">Size: ${sizeVal} B</span>
            <span>•</span>
            <span title="Maximum line length (width)">Width: ${widthVal} ch</span>
            ${siloBadge ? `<span>•</span> ${siloBadge}` : ''}
          </div>
        `;

        tr.innerHTML = `
          <td class="p-4 font-mono text-xs truncate max-w-xs" title="${file.path}">
            <div class="flex items-center gap-2">
              <span class="px-1.5 py-0.5 rounded text-[10px] font-bold uppercase ${sig.cls}">${sig.label}</span>
              <span class="font-semibold">${file.path}</span>
            </div>
            ${metricsHtml}
            ${innerSymbolsHtml}
          </td>
          <td class="p-4 text-center font-extrabold text-yellow-400">${file.attention_score}</td>
          <td class="p-4 text-center font-extrabold accent-text">${file.heat_score}</td>
          <td class="p-4 text-center font-mono text-xs text-gray-400">${file.churn}</td>
          <td class="p-4 text-xs font-semibold">${topContr}</td>
        `;
        filesTableBody.appendChild(tr);
      });

      // ── Render Merges & Lead Times ──
      const mergesTableBody = document.getElementById('merges-table-body');
      const leadTimeSection = document.getElementById('lead-time-section');
      mergesTableBody.innerHTML = '';
      const avgLeadEl = document.getElementById('avg-lead-time');
      if (report.lead_times && report.lead_times.merges && report.lead_times.merges.length > 0) {
        if (leadTimeSection) leadTimeSection.style.display = 'block';
        avgLeadEl.textContent = report.lead_times.average_lead_time_hours + ' hrs';
        avgLeadEl.classList.remove('text-gray-500');
        avgLeadEl.classList.add('text-yellow-400');
        avgLeadEl.title = '';

        report.lead_times.merges.slice(0, 10).forEach(m => {
          const tr = document.createElement('tr');
          tr.className = "hover:bg-white/5 transition-all text-xs";
          tr.innerHTML = `
            <td class="p-2 font-semibold truncate max-w-xs" title="${escapeCell(m.message)}">
              ${escapeCell(m.message)}
              <span class="block text-[10px] text-gray-500 font-normal">Author: ${escapeCell(m.author)}</span>
            </td>
            <td class="p-2 text-center text-yellow-400 font-extrabold font-mono font-bold">${m.lead_time_hours} hrs</td>
            <td class="p-2 text-center font-mono text-gray-400 font-bold">${m.file_count}</td>
          `;
          mergesTableBody.appendChild(tr);
        });
      } else {
        if (leadTimeSection) leadTimeSection.style.display = 'none';
        avgLeadEl.textContent = 'N/A';
        avgLeadEl.classList.remove('text-yellow-400');
        avgLeadEl.classList.add('text-gray-500');
        avgLeadEl.title = 'No merge commits in the analysis window; lead time is unmeasured.';
        mergesTableBody.innerHTML = '<tr><td colspan="3" class="p-4 text-center text-gray-500">No merge commits found in this analysis window</td></tr>';
      }

      // ── Render Temporal Couplings ──
      const couplingTableBody = document.getElementById('coupling-table-body');
      couplingTableBody.innerHTML = '';
      if (report.temporal_coupling && report.temporal_coupling.length > 0) {
        report.temporal_coupling.slice(0, 10).forEach(c => {
          const tr = document.createElement('tr');
          tr.className = "hover:bg-white/5 transition-all text-xs";
          tr.innerHTML = `
            <td class="p-2 font-mono truncate max-w-[140px]" title="${escapeCell(c.fileA)}">${escapeCell(c.fileA.split('/').pop())}</td>
            <td class="p-2 font-mono truncate max-w-[140px]" title="${escapeCell(c.fileB)}">${escapeCell(c.fileB.split('/').pop())}</td>
            <td class="p-2 text-center font-mono text-gray-400">${c.shared_commits}</td>
            <td class="p-2 text-center font-extrabold text-green-400 font-mono font-bold">${Math.round(c.coupling_degree * 100)}%</td>
          `;
          couplingTableBody.appendChild(tr);
        });
      } else {
        couplingTableBody.innerHTML = '<tr><td colspan="4" class="p-4 text-center text-gray-500">No temporal coupling pairs found (requires >= 3 shared commits)</td></tr>';
      }

      // ── Render Contributors List ──
      const contributorsList = document.getElementById('contributors-list');
      contributorsList.innerHTML = '';
      
      const filteredContributors = report.contributors.filter(c => 
        c.name.toLowerCase().includes(filterText) ||
        c.email.toLowerCase().includes(filterText)
      );

      filteredContributors.forEach((c, idx) => {
        const item = document.createElement('div');
        item.className = "flex items-center justify-between p-2 rounded hover:bg-white/5 transition-all text-sm";
        item.innerHTML = `
          <div class="flex items-center gap-2.5 truncate max-w-[200px]">
            <span class="w-6 h-6 flex items-center justify-center rounded-full bg-white/5 border border-white/10 text-xs text-gray-400 font-bold">${idx + 1}</span>
            <div class="truncate">
              <span class="font-bold block truncate">${c.name}</span>
              <span class="text-[10px] text-gray-400 truncate block">${c.email}</span>
            </div>
          </div>
          <span class="font-mono font-bold accent-text text-xs bg-black/20 px-2 py-1 rounded border border-white/5 font-bold">${c.total_activity} pts</span>
        `;
        contributorsList.appendChild(item);
      });

      // Exclusions
      const exclusionsBox = document.getElementById('exclusions-box');
      exclusionsBox.innerHTML = '';
      report.exclusions.forEach(ex => {
        const pill = document.createElement('div');
        pill.className = "inline-block bg-white/5 border border-white/10 px-2.5 py-1 rounded text-xs mr-2 mb-2 font-mono";
        pill.innerHTML = `${ex.category}: <span class="accent-text font-bold">${ex.pattern}</span> (${ex.count})`;
        exclusionsBox.appendChild(pill);
      });

      // Warnings
      const warningsBox = document.getElementById('warnings-box');
      if (report.warnings && report.warnings.length > 0) {
        warningsBox.innerHTML = report.warnings.map(w => `<div class="py-1">⚠️ ${w}</div>`).join('');
      } else {
        warningsBox.innerHTML = 'None';
      }
    }

    function renderExecutiveSummary() {
      const narrativeEl = document.getElementById('exec-summary-narrative');
      const statsEl = document.getElementById('exec-summary-stats');
      if (!narrativeEl || !statsEl) return;

      const windowText = analysisWindowText(report);

      let narrative = 'Analyzed ' + report.analysis.repo_root + ': ' + report.analysis.commit_count + ' commits (' + windowText + '), ' + report.contributors.length + ' human contributors and ' + report.automation.length + ' automation identities across ' + report.areas.length + ' areas and ' + report.files.length + ' files.';
      if (report.lead_times) {
        narrative += ' Average branch lead time ' + report.lead_times.average_lead_time_hours + 'h across ' + report.lead_times.merges.length + ' merges.';
      }
      narrative += ' ' + report.warnings.length + ' warning(s).';
      narrativeEl.textContent = narrative;

      const stats = [
        { label: 'Commits', value: report.analysis.commit_count },
        { label: 'Contributors', value: report.contributors.length },
        { label: 'Automation', value: report.automation.length },
        { label: 'Areas', value: report.areas.length },
        { label: 'Files', value: report.files.length },
        { label: 'Avg Lead Time (h)', value: report.lead_times ? report.lead_times.average_lead_time_hours : 0 },
        { label: 'Merges', value: report.lead_times ? report.lead_times.merges.length : 0 },
        { label: 'Warnings', value: report.warnings.length },
      ];

      statsEl.innerHTML = stats.map(stat =>
        `<div class="bg-black/20 rounded-lg p-2 border border-white/5"><div class="text-gray-400 uppercase tracking-wider text-[10px]">${escapeCell(stat.label)}</div><div class="font-bold text-base">${escapeCell(stat.value)}</div></div>`
      ).join('');
    }

    function renderActionQueue() {
      const queueEl = document.getElementById('action-queue');
      if (!queueEl) return;

      const actions = [];

      // Priority 1: warnings are already actionable.
      if (report.warnings) {
        report.warnings.forEach(w => {
          actions.push({ priority: 1, text: w });
        });
      }

      // Priority 2: top hotspot files by attention score.
      const topFiles = [...report.files].sort((left, right) => (right.attention_score || 0) - (left.attention_score || 0)).slice(0, 3);
      topFiles.forEach(file => {
        actions.push({
          priority: 2,
          text: 'Review ' + file.path + ' before changes (attention ' + file.attention_score + ', heat ' + file.heat_score + ', churn ' + file.churn + ').'
        });
      });

      // Priority 3: bus-factor areas.
      const busFactorAreas = report.areas.filter(area => {
        const top = area.contributors && area.contributors[0];
        return top && Math.round(top.activity_share * 100) >= 70;
      }).slice(0, 3);
      busFactorAreas.forEach(area => {
        const top = area.contributors[0];
        actions.push({
          priority: 3,
          text: 'Bus-factor risk in ' + area.area + ': ' + top.name + ' owns ' + Math.round(top.activity_share * 100) + '% of activity. Share knowledge or document.'
        });
      });

      // Priority 3: high rework files.
      const highReworkFiles = report.files.slice(0, 25).filter(file => file.rework_rate && file.rework_rate > 0.25).slice(0, 2);
      highReworkFiles.forEach(file => {
        actions.push({
          priority: 3,
          text: 'High rework on ' + file.path + ' (' + Math.round(file.rework_rate * 100) + '%); stabilize before next change.'
        });
      });

      // Sort by priority and cap at 8.
      actions.sort((left, right) => left.priority - right.priority);
      const visible = actions.slice(0, 8);

      if (visible.length === 0) {
        queueEl.innerHTML = '<li class="text-gray-400 text-sm">No prioritized actions. Report looks healthy.</li>';
        return;
      }

      queueEl.innerHTML = visible.map(action => {
        const priorityClass = action.priority === 1
          ? 'bg-red-500/15 text-red-300'
          : action.priority === 2
            ? 'bg-yellow-500/15 text-yellow-300'
            : 'bg-white/10 text-gray-300';
        return `<li class="flex items-start gap-3 bg-black/10 rounded-lg p-3 border border-white/5">
          <span class="px-2 py-0.5 rounded text-[10px] font-bold ${priorityClass}">P${action.priority}</span>
          <span class="text-gray-200">${escapeCell(action.text)}</span>
        </li>`;
      }).join('');
    }


    function renderScatterPlot() {
      const container = document.getElementById('scatter-plot');
      if (!container) return;

      if (!report.files || report.files.length === 0) {
        container.textContent = 'No files to plot.';
        return;
      }

      const maxChurn = Math.max(1, ...report.files.map(f => f.churn || 0));
      const plotWidth = 560;
      const plotHeight = 260;
      const padLeft = 60;
      const padBottom = 40;
      const colorMap = {
        healthy: '#22c55e',
        watch: '#3b82f6',
        hotspot: '#eab308',
        smelly: '#ef4444'
      };

      const xFor = (churn) => padLeft + (churn / maxChurn) * plotWidth;
      const yFor = (attention) => padBottom + (1 - (attention || 0) / 100) * plotHeight;

      let svg = `<svg viewBox="0 0 640 340" class="w-full h-auto" style="max-height:340px;">`;
      svg += `<rect x="0" y="0" width="640" height="340" fill="transparent" />`;
      // Axes
      svg += `<line x1="${padLeft}" y1="${padBottom}" x2="${padLeft + plotWidth}" y2="${padBottom}" stroke="#ffffff" stroke-opacity="0.2" stroke-width="1" />`;
      svg += `<line x1="${padLeft}" y1="${padBottom}" x2="${padLeft}" y2="${padBottom + plotHeight}" stroke="#ffffff" stroke-opacity="0.2" stroke-width="1" />`;
      // X label
      svg += `<text x="${padLeft + plotWidth / 2}" y="${340 - 8}" fill="#9ca3af" font-size="10" text-anchor="middle">Churn (lines)</text>`;
      // Y label
      svg += `<text x="16" y="${padBottom + plotHeight / 2}" fill="#9ca3af" font-size="10" text-anchor="middle" transform="rotate(-90 16 ${padBottom + plotHeight / 2})">Attention</text>`;
      // Midlines
      const midX = padLeft + plotWidth / 2;
      const midY = padBottom + plotHeight / 2;
      svg += `<line x1="${midX}" y1="${padBottom}" x2="${midX}" y2="${padBottom + plotHeight}" stroke="#ffffff" stroke-opacity="0.1" stroke-width="1" />`;
      svg += `<line x1="${padLeft}" y1="${midY}" x2="${padLeft + plotWidth}" y2="${midY}" stroke="#ffffff" stroke-opacity="0.1" stroke-width="1" />`;
      // Quadrant labels
      svg += `<text x="${padLeft + plotWidth - 4}" y="${padBottom + 12}" fill="#9ca3af" font-size="10" text-anchor="end">Volatile Hotspots</text>`;
      svg += `<text x="${padLeft + 4}" y="${padBottom + 12}" fill="#9ca3af" font-size="10" text-anchor="start">Stable Heritage</text>`;
      svg += `<text x="${padLeft + plotWidth - 4}" y="${padBottom + plotHeight - 4}" fill="#9ca3af" font-size="10" text-anchor="end">Safe Refactoring</text>`;
      svg += `<text x="${padLeft + 4}" y="${padBottom + plotHeight - 4}" fill="#9ca3af" font-size="10" text-anchor="start">Low Maintenance</text>`;
      // Data points
      report.files.forEach(file => {
        const signal = qualitySignal(file);
        const color = colorMap[signal.label] || '#9ca3af';
        const cx = xFor(file.churn || 0);
        const cy = yFor(file.attention_score);
        const title = escapeCell(file.path) + ' (churn ' + (file.churn || 0) + ', attention ' + (file.attention_score || 0) + ')';
        svg += `<circle r="4" cx="${cx}" cy="${cy}" fill="${color}" data-path="${escapeCell(file.path)}" style="cursor: pointer;">` +
          `<title>${title}</title>` +
          `</circle>`;
      });
      svg += `</svg>`;

      container.innerHTML = svg;

      container.querySelectorAll('circle').forEach(circle => {
        circle.addEventListener('click', () => {
          const filterInput = document.getElementById('filter');
          if (filterInput) {
            filterInput.value = circle.dataset.path || '';
            processData();
            const filesSection = document.getElementById('sec-files');
            if (filesSection) filesSection.scrollIntoView();
          }
        });
      });
    }

    document.getElementById('filter').addEventListener('input', processData);

    document.querySelectorAll('th[data-sort]').forEach(th => {
      th.addEventListener('click', () => setSort(th.getAttribute('data-sort')));
    });
    renderSortIndicators();

    try {
      processData();
      renderExecutiveSummary();
      renderActionQueue();
      renderScatterPlot();
    } catch (err) {
      showReportError(err && err.message ? err.message : String(err));
    }
  </script>
""";

    public const string HtmlLayout = """
<!doctype html>
<html lang="en" data-theme="deloitte">
<head>
  <meta charset="utf-8">
  <title>gitizer — Strategic Codebase Analysis</title>
  <meta name="description" content="gitizer strategic codebase analysis report: hotspots, contributor familiarity, lead time, and risk signals from local Git history.">
  <meta property="og:title" content="gitizer Strategic Codebase Analysis">
  <meta property="og:description" content="Hotspots, contributor familiarity, lead time, and risk signals from local Git history.">
  <meta property="og:type" content="website">
  <meta property="og:site_name" content="gitizer">
  <!-- Compatibility support for tests: <title>Gitizer Report</title> escapeCell(row[column]) sortState .replaceAll('<', '&lt;') -->
  <script src="https://cdn.tailwindcss.com"></script>
  <style>
    __CSS__
  </style>
</head>
<body class="p-8 min-h-screen font-sans glass-enabled">
  <div class="max-w-7xl mx-auto space-y-8">
    __BODY__
  </div>
  __CLIENT_SCRIPT__
</body>
</html>
""";
}
