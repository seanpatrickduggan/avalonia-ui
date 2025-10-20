# Current Tasks

## Active Task: Fix CI/CD UI Test Failures

### Problem
GitHub Actions CI/CD is failing UI tests due to workspace configuration requirements in headless Avalonia tests.

**Error Details:**
```
Failed FileProcessor.UI.Tests.Converters.ConvertersTests.LogSeverityToBrushConverter_Convert_InvalidValue_ReturnsGrayBrush [1 ms]
Error Message:
System.InvalidOperationException : No workspace configured. Please configure a workspace before starting the application.
Stack Trace:
   at FileProcessor.UI.App.ConfigureLogging() in /home/runner/work/avalonia-ui/avalonia-ui/FileProcessor.UI/App.axaml.cs:line 44
   at FileProcessor.UI.App.OnFrameworkInitializationCompleted() in /home/runner/work/avalonia-ui/avalonia-ui/FileProcessor.UI/App.axaml.cs:line 70
   at Avalonia.AppBuilder.SetupUnsafe()
   at Avalonia.Headless.HeadlessUnitTestSession.EnsureApplication()
   at Avalonia.Headless.HeadlessUnitTestSession.<>c__DisplayClass10_0`1.<DispatchCore>b__0()
```

### Root Cause
- UI tests use `Avalonia.Headless.XUnit` which initializes the Avalonia application
- `App.axaml.cs:OnFrameworkInitializationCompleted()` calls `ConfigureLogging()`
- `ConfigureLogging()` requires a workspace to be configured (via `SettingsService.Instance.WorkspaceDirectory`)
- CI environment has no workspace configured, causing test failures

### Impact
- CI/CD pipeline fails on UI tests
- Blocks automated testing and deployment
- UI test coverage cannot be validated in CI

### Solution Approach
1. **Option A**: Modify `AvaloniaTestSetup.cs` to provide a mock/fake workspace before app initialization
2. **Option B**: Refactor `App.axaml.cs` to make logging configuration optional or lazy for test environments
3. **Option C**: Create test-specific app builder that bypasses workspace-dependent initialization

### Acceptance Criteria
- All UI tests pass in CI/CD environment
- UI tests can run headless without workspace configuration
- Local development testing remains unaffected
- CI pipeline shows green status for UI test suite

### Next Steps
- Analyze current `AvaloniaTestSetup.cs` and `App.axaml.cs` initialization flow
- Implement solution to provide mock workspace or bypass workspace requirement for tests
- Test locally then verify CI passes
- Document the fix in testing architecture
