# 📊 Gitic Analysis Report: sessions-db
Generated on: 2026-07-25 07:57:30

## 📈 Repository Overview
- **Repository Root:** `/Users/kfchen/Desktop/ca/chenze/sessions-db`
- **Analysis Command:** `Report`
- **Total Files Analyzed:** 233
- **Total Contributors:** 2
- **Time Window Filter:** Default Window

## 🔥 Top Code Hotspots & Attention Metrics
These files have the highest **Attention Score**, which combines change recency, churn volume, code complexity (file length/size), and contributor dispersion to find code needing active review.

### 📊 Visual Hotspot Quadrant Map
Hover over the circles to view file metrics. Larger circles indicate larger file sizes. Red/Orange points denote high attention/rework.

<svg viewBox="0 0 800 450" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg" style="background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;">
  <defs>
    <linearGradient id="bgGrad" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="#0f172a" />
      <stop offset="100%" stop-color="#1e293b" />
    </linearGradient>
  </defs>
  <rect width="800" height="450" fill="url(#bgGrad)" rx="8" />
  <rect x="410" y="50" width="340" height="170" fill="#ef4444" fill-opacity="0.02" />
  <rect x="70" y="50" width="340" height="170" fill="#f59e0b" fill-opacity="0.01" />
  <rect x="410" y="220" width="340" height="170" fill="#3b82f6" fill-opacity="0.01" />
  <rect x="70" y="220" width="340" height="170" fill="#10b981" fill-opacity="0.01" />
  <line x1="410" y1="50" x2="410" y2="390" stroke="#334155" stroke-dasharray="4 4" stroke-width="1" opacity="0.5" />
  <line x1="70" y1="220" x2="750" y2="220" stroke="#334155" stroke-dasharray="4 4" stroke-width="1" opacity="0.5" />
  <line x1="70" y1="390" x2="750" y2="390" stroke="#475569" stroke-width="1.5" />
  <line x1="70" y1="50" x2="70" y2="390" stroke="#475569" stroke-width="1.5" />
  <text x="740" y="70" fill="#ef4444" font-size="11" font-weight="bold" text-anchor="end" opacity="0.7">🔥 Volatile Hotspots</text>
  <text x="80" y="70" fill="#f59e0b" font-size="11" font-weight="bold" text-anchor="start" opacity="0.7">📦 Complex Heritage</text>
  <text x="740" y="375" fill="#3b82f6" font-size="11" font-weight="bold" text-anchor="end" opacity="0.7">⚡ Active Refactoring</text>
  <text x="80" y="375" fill="#10b981" font-size="11" font-weight="bold" text-anchor="start" opacity="0.7">🌿 Low Maintenance</text>
  <text x="410" y="435" fill="#94a3b8" font-size="12" text-anchor="middle" font-weight="500">Churn Volume (lines changed)</text>
  <text x="18" y="220" fill="#94a3b8" font-size="12" text-anchor="middle" transform="rotate(-90 18 220)" font-weight="500">Attention Score (0 - 100)</text>
  <line x1="66" y1="390" x2="70" y2="390" stroke="#475569" stroke-width="1.5" />
  <text x="62" y="394" fill="#64748b" font-size="10" text-anchor="end">0</text>
  <line x1="66" y1="305" x2="70" y2="305" stroke="#475569" stroke-width="1.5" />
  <text x="62" y="309" fill="#64748b" font-size="10" text-anchor="end">25</text>
  <line x1="66" y1="220" x2="70" y2="220" stroke="#475569" stroke-width="1.5" />
  <text x="62" y="224" fill="#64748b" font-size="10" text-anchor="end">50</text>
  <line x1="66" y1="135" x2="70" y2="135" stroke="#475569" stroke-width="1.5" />
  <text x="62" y="139" fill="#64748b" font-size="10" text-anchor="end">75</text>
  <line x1="66" y1="50" x2="70" y2="50" stroke="#475569" stroke-width="1.5" />
  <text x="62" y="54" fill="#64748b" font-size="10" text-anchor="end">100</text>
  <line x1="70" y1="390" x2="70" y2="394" stroke="#475569" stroke-width="1.5" />
  <text x="70" y="408" fill="#64748b" font-size="10" text-anchor="middle">0</text>
  <line x1="410" y1="390" x2="410" y2="394" stroke="#475569" stroke-width="1.5" />
  <text x="410" y="408" fill="#64748b" font-size="10" text-anchor="middle">2840</text>
  <line x1="750" y1="390" x2="750" y2="394" stroke="#475569" stroke-width="1.5" />
  <text x="750" y="408" fill="#64748b" font-size="10" text-anchor="middle">5681</text>
  <circle cx="115.8" cy="182.6" r="6.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/SpanInspector.css
Attention: 61.0
Lines: 384
Churn: 383
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.2" cy="182.6" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/ErrorBoundary.test.tsx
Attention: 61.0
Lines: 36
Churn: 35
Rework Rate: 100.0%</title>
  </circle>
  <circle cx="85.7" cy="182.6" r="4.8" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/ContextGraph.test.tsx
Attention: 61.0
Lines: 132
Churn: 131
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.3" cy="182.6" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IAgentLogParser.cs
Attention: 61.0
Lines: 12
Churn: 11
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="77.9" cy="182.6" r="4.4" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>GEMINI.md
Attention: 61.0
Lines: 67
Churn: 66
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.7" cy="182.6" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>deepening_plan.md
Attention: 61.0
Lines: 40
Churn: 39
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="77.2" cy="179.2" r="4.4" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/MarkdownRenderer.test.tsx
Attention: 62.0
Lines: 61
Churn: 60
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="129.5" cy="179.2" r="7.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/BenchmarkRaceTrack.css
Attention: 62.0
Lines: 498
Churn: 497
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="73.6" cy="179.2" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/RegexPatterns.cs
Attention: 62.0
Lines: 31
Churn: 30
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.9" cy="179.2" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/MultiAgentSessionHandler.cs
Attention: 62.0
Lines: 42
Churn: 41
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="75.9" cy="179.2" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/JsonHelpers.cs
Attention: 62.0
Lines: 50
Churn: 49
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="73.7" cy="179.2" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/DefaultTransformationHelper.cs
Attention: 62.0
Lines: 32
Churn: 31
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.1" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/ITurnReconstructor.cs
Attention: 62.0
Lines: 10
Churn: 9
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.6" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/ITransformationHelper.cs
Attention: 62.0
Lines: 14
Churn: 13
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.1" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/ISpanMapper.cs
Attention: 62.0
Lines: 10
Churn: 9
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="72.0" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IRateCardProvider.cs
Attention: 62.0
Lines: 18
Churn: 17
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.3" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IPricingEngine.cs
Attention: 62.0
Lines: 12
Churn: 11
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.6" cy="179.2" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IIngestionEngine.cs
Attention: 62.0
Lines: 14
Churn: 13
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.7" cy="179.2" r="4.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IAgentTypeResolver.cs
Attention: 62.0
Lines: 7
Churn: 6
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.3" cy="179.2" r="4.4" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>run_loops.py
Attention: 62.0
Lines: 64
Churn: 36
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.0" cy="175.8" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/turnGrouping.ts
Attention: 63.0
Lines: 34
Churn: 33
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.8" cy="175.8" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/pathHelpers.ts
Attention: 63.0
Lines: 41
Churn: 40
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="76.5" cy="175.8" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/pathHelpers.test.ts
Attention: 63.0
Lines: 55
Churn: 54
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.9" cy="175.8" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/contextGraphHelpers.ts
Attention: 63.0
Lines: 42
Churn: 41
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="80.9" cy="175.8" r="4.6" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/activityFinders.ts
Attention: 63.0
Lines: 92
Churn: 91
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="77.7" cy="175.8" r="4.4" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/SessionViewStateProvider.tsx
Attention: 63.0
Lines: 67
Churn: 64
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="77.8" cy="175.8" r="4.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/SessionViewStateContext.ts
Attention: 63.0
Lines: 84
Churn: 65
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.7" cy="175.8" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/PricingProvider.tsx
Attention: 63.0
Lines: 41
Churn: 39
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.9" cy="175.8" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/PricingContext.ts
Attention: 63.0
Lines: 42
Churn: 41
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="72.4" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/LayoutProvider.tsx
Attention: 63.0
Lines: 23
Churn: 20
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="72.2" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/context/LayoutContext.ts
Attention: 63.0
Lines: 19
Churn: 18
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="80.4" cy="175.8" r="4.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/TaskNotificationCard.tsx
Attention: 63.0
Lines: 88
Churn: 87
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="78.9" cy="175.8" r="4.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/ErrorBoundary.css
Attention: 63.0
Lines: 75
Churn: 74
Rework Rate: 100.0%</title>
  </circle>
  <circle cx="83.6" cy="175.8" r="4.7" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/EngineContextPanelMockData.ts
Attention: 63.0
Lines: 115
Churn: 114
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="160.3" cy="175.8" r="8.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/EngineContextPanel.css
Attention: 63.0
Lines: 755
Churn: 754
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.2" cy="175.8" r="4.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Infrastructure/FileSystem/PhysicalFileSystem.cs
Attention: 63.0
Lines: 52
Churn: 2
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.5" cy="175.8" r="4.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/GlobalUsings.cs
Attention: 63.0
Lines: 4
Churn: 4
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="452.3" cy="175.8" r="9.9" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/TransformationHelperBase.cs
Attention: 63.0
Lines: 987
Churn: 3194
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.3" cy="175.8" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/AgentContextTrackerBase.cs
Attention: 63.0
Lines: 37
Churn: 36
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.2" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/ParsedActivitiesResult.cs
Attention: 63.0
Lines: 10
Churn: 10
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.1" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IMultiAgentSessionHandler.cs
Attention: 63.0
Lines: 10
Churn: 9
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.3" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IModelContextTracker.cs
Attention: 63.0
Lines: 12
Churn: 11
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.9" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IFileSystem.cs
Attention: 63.0
Lines: 17
Churn: 16
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.7" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IEventStreamReader.cs
Attention: 63.0
Lines: 15
Churn: 14
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.1" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IDefaultTransformationHelper.cs
Attention: 63.0
Lines: 10
Churn: 9
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.2" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IAgentTransformationHelper.cs
Attention: 63.0
Lines: 11
Churn: 10
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.1" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IAgentActivityParser.cs
Attention: 63.0
Lines: 10
Churn: 9
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.7" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Contracts/UnifiedEvent.cs
Attention: 63.0
Lines: 15
Churn: 14
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.0" cy="175.8" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Contracts/TokenCount.cs
Attention: 63.0
Lines: 9
Churn: 8
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.7" cy="175.8" r="4.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Contracts/ContextSnapshot.cs
Attention: 63.0
Lines: 6
Churn: 6
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="74.2" cy="175.8" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/ActivityParserTests.cs
Attention: 63.0
Lines: 36
Churn: 35
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.5" cy="175.8" r="4.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>.gitattributes
Attention: 63.0
Lines: 4
Churn: 4
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="70.5" cy="175.8" r="4.0" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>.editorconfig
Attention: 63.0
Lines: 5
Churn: 4
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="100.6" cy="172.4" r="5.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/turnClassifier.test.ts
Attention: 64.0
Lines: 257
Churn: 256
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="92.9" cy="172.4" r="5.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/utils/timelineHelpers.test.ts
Attention: 64.0
Lines: 192
Churn: 191
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="81.0" cy="169.0" r="4.6" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/BundleManager.test.tsx
Attention: 65.0
Lines: 93
Churn: 92
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="76.9" cy="169.0" r="4.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Infrastructure/Storage/LocalBlobStorageService.cs
Attention: 65.0
Lines: 75
Churn: 58
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="78.4" cy="169.0" r="4.4" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Infrastructure/Storage/JsonBundleRepository.cs
Attention: 65.0
Lines: 71
Churn: 70
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="73.0" cy="169.0" r="4.2" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/ReviewQueue.cs
Attention: 65.0
Lines: 26
Churn: 25
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.8" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IShareRepository.cs
Attention: 65.0
Lines: 16
Churn: 15
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.2" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IReviewQueue.cs
Attention: 65.0
Lines: 11
Churn: 10
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="72.4" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IProviderPolicyService.cs
Attention: 65.0
Lines: 21
Churn: 20
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="72.5" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IBundleRepository.cs
Attention: 65.0
Lines: 22
Churn: 21
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.2" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IBlobStorageService.cs
Attention: 65.0
Lines: 11
Churn: 10
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.8" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Interfaces/IAdminRepository.cs
Attention: 65.0
Lines: 16
Churn: 15
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="84.4" cy="169.0" r="4.8" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/UploadAndStorageTests.cs
Attention: 65.0
Lines: 132
Churn: 120
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="82.9" cy="169.0" r="4.7" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/EvidenceIdentityTests.cs
Attention: 65.0
Lines: 109
Churn: 108
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="71.9" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>evaluation_results.json
Attention: 65.0
Lines: 16
Churn: 16
Rework Rate: 100.0%</title>
  </circle>
  <circle cx="71.9" cy="169.0" r="4.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>.scratch/session-review-coach/issues/13-launch-quality-evaluation-harness.md
Attention: 65.0
Lines: 17
Churn: 16
Rework Rate: 100.0%</title>
  </circle>
  <circle cx="91.1" cy="165.6" r="5.1" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/BundleManager.tsx
Attention: 66.0
Lines: 177
Churn: 176
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="94.9" cy="165.6" r="5.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/BundleManager.css
Attention: 66.0
Lines: 209
Churn: 208
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="96.1" cy="165.6" r="5.3" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Infrastructure/Storage/JsonShareRepository.cs
Attention: 66.0
Lines: 219
Churn: 218
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="87.4" cy="165.6" r="4.9" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Infrastructure/Storage/JsonAdminRepository.cs
Attention: 66.0
Lines: 146
Churn: 145
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="80.4" cy="165.6" r="4.5" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/ProviderPolicyService.cs
Attention: 66.0
Lines: 88
Churn: 87
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="88.2" cy="165.6" r="4.9" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/RoleScopedSharingTests.cs
Attention: 66.0
Lines: 153
Churn: 152
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="83.6" cy="165.6" r="4.7" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/ReviewHistoryAndFeedbackTests.cs
Attention: 66.0
Lines: 115
Churn: 114
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="86.6" cy="165.6" r="4.8" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/AdminReviewOperationsTests.cs
Attention: 66.0
Lines: 140
Churn: 139
Rework Rate: 0.0%</title>
  </circle>
  <circle cx="140.9" cy="158.8" r="7.6" fill="#eab308" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend.Tests/LaunchQualityEvaluationHarnessTests.cs
Attention: 68.0
Lines: 593
Churn: 592
Rework Rate: 100.0%</title>
  </circle>
  <circle cx="582.4" cy="145.2" r="5.8" fill="#ef4444" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Backend/Core/Services/TurnReconstructor.cs
Attention: 72.0
Lines: 301
Churn: 4281
Rework Rate: 23.0%</title>
  </circle>
  <circle cx="750.0" cy="124.8" r="7.6" fill="#f59e0b" fill-opacity="0.75" stroke="#1e293b" stroke-width="1">
    <title>src/KfcTelemetry.Frontend/src/components/TurnLog.tsx
Attention: 78.0
Lines: 597
Churn: 5681
Rework Rate: 12.0%</title>
  </circle>
  <line x1="750" y1="124.8" x2="738" y2="112.8" stroke="#94a3b8" stroke-width="0.8" opacity="0.6" />
  <text x="735" y="116.8" fill="#f1f5f9" font-size="9" font-weight="bold" text-anchor="end" opacity="0.95">TurnLog.tsx</text>
  <line x1="582.4238690371413" y1="145.2" x2="570.4238690371413" y2="157.2" stroke="#94a3b8" stroke-width="0.8" opacity="0.6" />
  <text x="567.4238690371413" y="161.2" fill="#f1f5f9" font-size="9" font-weight="bold" text-anchor="end" opacity="0.95">TurnReconstructor.cs</text>
  <line x1="140.8607639500088" y1="158.79999999999998" x2="152.8607639500088" y2="146.79999999999998" stroke="#94a3b8" stroke-width="0.8" opacity="0.6" />
  <text x="155.8607639500088" y="150.79999999999998" fill="#f1f5f9" font-size="9" font-weight="bold" text-anchor="start" opacity="0.95">LaunchQualityEvaluationHarnessTests.cs</text>
  <line x1="86.63791585988382" y1="165.6" x2="98.63791585988382" y2="177.6" stroke="#94a3b8" stroke-width="0.8" opacity="0.6" />
  <text x="101.63791585988382" y="181.6" fill="#f1f5f9" font-size="9" font-weight="bold" text-anchor="start" opacity="0.95">AdminReviewOperationsTests.cs</text>
  <line x1="83.64548494983278" y1="165.6" x2="95.64548494983278" y2="153.6" stroke="#94a3b8" stroke-width="0.8" opacity="0.6" />
  <text x="98.64548494983278" y="157.6" fill="#f1f5f9" font-size="9" font-weight="bold" text-anchor="start" opacity="0.95">ReviewHistoryAndFeedbackTests.cs</text>
</svg>


### 📊 Complexity Distribution (Min / Max / Avg)
Shows the span of file lengths (in lines) and max line widths (in characters) across the codebase.

<svg viewBox="0 0 800 330" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg" style="background-color:#0f172a; border-radius:8px; border:1px solid #1e293b; font-family:system-ui, -apple-system, sans-serif;">
  <defs>
    <linearGradient id="linesGrad" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0%" stop-color="#3b82f6" stop-opacity="0.4" />
      <stop offset="100%" stop-color="#3b82f6" stop-opacity="0.9" />
    </linearGradient>
    <linearGradient id="widthGrad" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0%" stop-color="#10b981" stop-opacity="0.4" />
      <stop offset="100%" stop-color="#10b981" stop-opacity="0.9" />
    </linearGradient>
  </defs>
  <text x="20" y="30" fill="#f1f5f9" font-size="14" font-weight="bold">📏 Complexity Distribution by App Module</text>
  <text x="20" y="55" fill="#64748b" font-size="10" font-weight="bold">MODULE / DIRECTORY</text>
  <text x="315" y="55" fill="#3b82f6" font-size="10" font-weight="bold" text-anchor="middle">FILE LENGTH (LINES) [Max: 1165]</text>
  <text x="625" y="55" fill="#10b981" font-size="10" font-weight="bold" text-anchor="middle">MAX LINE WIDTH (CHARS) [Max: 9522]</text>
  <line x1="180" y1="60" x2="180" y2="295" stroke="#1e293b" stroke-width="1" />
  <line x1="450" y1="60" x2="450" y2="295" stroke="#1e293b" stroke-width="1" opacity="0.5" />
  <line x1="490" y1="60" x2="490" y2="295" stroke="#1e293b" stroke-width="1" />
  <line x1="760" y1="60" x2="760" y2="295" stroke="#1e293b" stroke-width="1" opacity="0.5" />
  <rect x="10" y="60" width="780" height="45" fill="transparent" fill-opacity="0.15" rx="4" />
  <text x="20" y="83" fill="#f1f5f9" font-size="11" font-weight="bold">src/KfcTelemetry.Backend</text>
  <text x="20" y="95" fill="#64748b" font-size="9">98 files, 449 touches</text>
  <rect x="180" y="75" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="180.9" y="75" width="241.0" height="12" fill="url(#linesGrad)" rx="3" />
  <line x1="203.9" y1="72" x2="203.9" y2="90" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="203.9" cy="81" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="203.9" y="69" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">103</text>
  <text x="180" y="99" fill="#475569" font-size="8">Min: 4</text>
  <text x="450" y="99" fill="#475569" font-size="8" text-anchor="end">Max: 1044</text>
  <rect x="490" y="75" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="491.1" y="75" width="7.3" height="12" fill="url(#widthGrad)" rx="3" />
  <line x1="493.5" y1="72" x2="493.5" y2="90" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="493.5" cy="81" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="493.5" y="69" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">122</text>
  <text x="490" y="99" fill="#475569" font-size="8">Min: 38</text>
  <text x="760" y="99" fill="#475569" font-size="8" text-anchor="end">Max: 295</text>
  <rect x="10" y="105" width="780" height="45" fill="#1e293b" fill-opacity="0.15" rx="4" />
  <text x="20" y="128" fill="#f1f5f9" font-size="11" font-weight="bold">...KfcTelemetry.Frontend</text>
  <text x="20" y="140" fill="#64748b" font-size="9">94 files, 282 touches</text>
  <rect x="180" y="120" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="180.2" y="120" width="269.8" height="12" fill="url(#linesGrad)" rx="3" />
  <line x1="220.6" y1="117" x2="220.6" y2="135" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="220.6" cy="126" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="220.6" y="114" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">175</text>
  <text x="180" y="144" fill="#475569" font-size="8">Min: 1</text>
  <text x="450" y="144" fill="#475569" font-size="8" text-anchor="end">Max: 1165</text>
  <rect x="490" y="120" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="490.9" y="120" width="269.1" height="12" fill="url(#widthGrad)" rx="3" />
  <line x1="500.5" y1="117" x2="500.5" y2="135" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="500.5" cy="126" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="500.5" y="114" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">370</text>
  <text x="490" y="144" fill="#475569" font-size="8">Min: 30</text>
  <text x="760" y="144" fill="#475569" font-size="8" text-anchor="end">Max: 9522</text>
  <rect x="10" y="150" width="780" height="45" fill="transparent" fill-opacity="0.15" rx="4" />
  <text x="20" y="173" fill="#f1f5f9" font-size="11" font-weight="bold">...lemetry.Backend.Tests</text>
  <text x="20" y="185" fill="#64748b" font-size="9">21 files, 99 touches</text>
  <rect x="180" y="165" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="186.5" y="165" width="147.2" height="12" fill="url(#linesGrad)" rx="3" />
  <line x1="231.2" y1="162" x2="231.2" y2="180" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="231.2" cy="171" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="231.2" y="159" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">221</text>
  <text x="180" y="189" fill="#475569" font-size="8">Min: 28</text>
  <text x="450" y="189" fill="#475569" font-size="8" text-anchor="end">Max: 663</text>
  <rect x="490" y="165" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="492.1" y="165" width="9.3" height="12" fill="url(#widthGrad)" rx="3" />
  <line x1="495.4" y1="162" x2="495.4" y2="180" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="495.4" cy="171" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="495.4" y="159" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">190</text>
  <text x="490" y="189" fill="#475569" font-size="8">Min: 73</text>
  <text x="760" y="189" fill="#475569" font-size="8" text-anchor="end">Max: 401</text>
  <rect x="10" y="195" width="780" height="45" fill="#1e293b" fill-opacity="0.15" rx="4" />
  <text x="20" y="218" fill="#f1f5f9" font-size="11" font-weight="bold">[Root Directory]</text>
  <text x="20" y="230" fill="#64748b" font-size="9">13 files, 17 touches</text>
  <rect x="180" y="210" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="180.7" y="210" width="28.5" height="12" fill="url(#linesGrad)" rx="3" />
  <line x1="189.2" y1="207" x2="189.2" y2="225" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="189.2" cy="216" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="189.2" y="204" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">40</text>
  <text x="180" y="234" fill="#475569" font-size="8">Min: 3</text>
  <text x="450" y="234" fill="#475569" font-size="8" text-anchor="end">Max: 126</text>
  <rect x="490" y="210" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="490.5" y="210" width="6.7" height="12" fill="url(#widthGrad)" rx="3" />
  <line x1="492.8" y1="207" x2="492.8" y2="225" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="492.8" cy="216" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="492.8" y="204" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">98</text>
  <text x="490" y="234" fill="#475569" font-size="8">Min: 17</text>
  <text x="760" y="234" fill="#475569" font-size="8" text-anchor="end">Max: 255</text>
  <rect x="10" y="240" width="780" height="45" fill="transparent" fill-opacity="0.15" rx="4" />
  <text x="20" y="263" fill="#f1f5f9" font-size="11" font-weight="bold">...Telemetry.Transformer</text>
  <text x="20" y="275" fill="#64748b" font-size="9">2 files, 24 touches</text>
  <rect x="180" y="255" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="183.7" y="255" width="49.8" height="12" fill="url(#linesGrad)" rx="3" />
  <line x1="208.6" y1="252" x2="208.6" y2="270" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="208.6" cy="261" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="208.6" y="249" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">124</text>
  <text x="180" y="279" fill="#475569" font-size="8">Min: 16</text>
  <text x="450" y="279" fill="#475569" font-size="8" text-anchor="end">Max: 231</text>
  <rect x="490" y="255" width="270" height="12" fill="#1e293b" rx="3" />
  <rect x="492.4" y="255" width="3.5" height="12" fill="url(#widthGrad)" rx="3" />
  <line x1="494.2" y1="252" x2="494.2" y2="270" stroke="#f43f5e" stroke-width="1.5" />
  <circle cx="494.2" cy="261" r="3.5" fill="#f43f5e" stroke="#f1f5f9" stroke-width="1" />
  <text x="494.2" y="249" fill="#f43f5e" font-size="8" font-weight="bold" text-anchor="middle">147</text>
  <text x="490" y="279" fill="#475569" font-size="8">Min: 86</text>
  <text x="760" y="279" fill="#475569" font-size="8" text-anchor="end">Max: 208</text>
  <text x="400" y="315" fill="#64748b" font-size="9" text-anchor="middle">Comparative horizontal scale relative to overall codebase maxima. Magenta pins indicate the average complexity per module.</text>
</svg>


| File Path | Lines | Size (KB) | Churn | Rework Rate | Attention Score | Major Risk Signals |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| `src/KfcTelemetry.Frontend/src/components/TurnLog.tsx` | 597 | 24.0 | 5681 | 12.0% | 78.0 | Knowledge Silo (92%) |
| `src/KfcTelemetry.Backend/Core/Services/TurnReconstructor.cs` | 301 | 10.8 | 4281 | 23.0% | 72.0 | High Rework (23.0%), Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend.Tests/LaunchQualityEvaluationHarnessTests.cs` | 593 | 29.6 | 592 | 100.0% | 68.0 | High Rework (100.0%), Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend.Tests/ReviewHistoryAndFeedbackTests.cs` | 115 | 3.6 | 114 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend.Tests/AdminReviewOperationsTests.cs` | 140 | 4.3 | 139 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend/Infrastructure/Storage/JsonAdminRepository.cs` | 146 | 4.5 | 145 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend.Tests/RoleScopedSharingTests.cs` | 153 | 5.4 | 152 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend/Infrastructure/Storage/JsonShareRepository.cs` | 219 | 7.0 | 218 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend/Core/Services/ProviderPolicyService.cs` | 88 | 3.0 | 87 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Frontend/src/components/BundleManager.css` | 209 | 3.0 | 208 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Frontend/src/components/BundleManager.tsx` | 177 | 6.8 | 176 | 0.0% | 66.0 | Knowledge Silo (100%) |
| `.scratch/session-review-coach/issues/13-launch-quality-evaluation-harness.md` | 17 | 1.6 | 16 | 100.0% | 65.0 | High Rework (100.0%), Knowledge Silo (100%) |
| `evaluation_results.json` | 16 | 0.5 | 16 | 100.0% | 65.0 | High Rework (100.0%), Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend/Core/Interfaces/IAdminRepository.cs` | 16 | 0.5 | 15 | 0.0% | 65.0 | Knowledge Silo (100%) |
| `src/KfcTelemetry.Backend/Core/Interfaces/IShareRepository.cs` | 16 | 0.6 | 15 | 0.0% | 65.0 | Knowledge Silo (100%) |

### ⚠️ Key Signal Highlights
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Frontend/src/components/TurnLog.tsx` is authored **92%** by a single developer. This presents key-person risk if they are unavailable.
- ⚠️ **Rework Alert:** `src/KfcTelemetry.Backend/Core/Services/TurnReconstructor.cs` has a rework rate of **23.0%**. A significant portion of its churn is revisions of recent commits, which often signals unstable requirements or architectural fragility.
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Backend/Core/Services/TurnReconstructor.cs` is authored **100%** by a single developer. This presents key-person risk if they are unavailable.
- ⚠️ **Rework Alert:** `src/KfcTelemetry.Backend.Tests/LaunchQualityEvaluationHarnessTests.cs` has a rework rate of **100.0%**. A significant portion of its churn is revisions of recent commits, which often signals unstable requirements or architectural fragility.
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Backend.Tests/LaunchQualityEvaluationHarnessTests.cs` is authored **100%** by a single developer. This presents key-person risk if they are unavailable.
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Backend.Tests/ReviewHistoryAndFeedbackTests.cs` is authored **100%** by a single developer. This presents key-person risk if they are unavailable.
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Backend.Tests/AdminReviewOperationsTests.cs` is authored **100%** by a single developer. This presents key-person risk if they are unavailable.
- 👤 **Knowledge Silo Alert:** `src/KfcTelemetry.Backend/Infrastructure/Storage/JsonAdminRepository.cs` is authored **100%** by a single developer. This presents key-person risk if they are unavailable.

## 👥 Top Contributors & Ownership
Contributors ordered by total repository touch activity.

| Contributor | Email | Activity Touches | Top Impact Areas |
| :--- | :--- | :---: | :--- |
| **KFC** | `kfc@chen` | 869 | `src/KfcTelemetry.Backend` (100%), `src/KfcTelemetry.Backend.Tests` (100%) |
| **kfc** | `kfc@kfc` | 10 | `.` (18%), `src/KfcTelemetry.Frontend` (2%) |

## 📁 Module / Area Ownership
Code directories analyzed by touch counts and ownership spread.

| Directory | File Count | Touches | Churn | Top Contributor / Ownership |
| :--- | :---: | :---: | :---: | :--- |
| `src/KfcTelemetry.Backend` | 98 | 449 | 25233 | **KFC** (100%) |
| `src/KfcTelemetry.Frontend` | 94 | 282 | 25959 | **KFC** (98%) |
| `src/KfcTelemetry.Backend.Tests` | 21 | 99 | 5010 | **KFC** (100%) |
| `src/KfcTelemetry.Transformer` | 2 | 24 | 397 | **KFC** (100%) |
| `.` | 13 | 17 | 507 | **KFC** (82%) |
| `handoffs` | 2 | 4 | 167 | **KFC** (100%) |
| `src` | 2 | 3 | 66 | **KFC** (100%) |
| `.scratch/session-review-coach` | 1 | 1 | 16 | **KFC** (100%) |

## ⚠️ Warnings & Recommendations
- 9 file(s) have single-touch high churn (>200 lines) with a single author. These may be generated files or scaffolding. Consider adding them to your .gitizer.yml excludes.
- No bots are configured and no automation identities were detected. If this repository has CI or release bots, configure them in .gitizer.yml.
- No merge commits in the analysis window; branch lead time is unmeasured. Run with --include-merges or widen the window to measure lead time.

---
*Report generated by **Gitic** — Gitizer C# Port (v0.1.0)*
