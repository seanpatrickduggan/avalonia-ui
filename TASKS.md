# Tasks

## Current

**LogViewer Usability Improvements** - Identified during code review (2026-03-01):

### Tier 1: High Value
- [ ] **Fix Data search inconsistency** - Standalone viewer only searches message; embedded searches message + data. Make consistent.
- [ ] **Add Min/Max severity dropdowns** - Replace 6 checkboxes with 2 dropdowns ("Show Warning and above")
- [ ] **Add time range picker** - Database supports it, UI doesn't expose it
- [ ] **Add results summary panel** - Show count breakdown by severity at a glance

### Tier 2: Quick Wins
- [ ] Copy entry context menu (right-click → Copy JSON / Copy Message)
- [ ] Search match highlighting in results
- [ ] Clear search button (X icon)
- [ ] Collapsible advanced filters panel

### Tier 3: Nice to Have
- [ ] Multi-select categories
- [ ] Grouping options (by severity, not just category)
- [ ] Export format options (CSV, plain text)
- [ ] Keyboard shortcuts (Ctrl+F, Ctrl+E)

---

**CI/CD UI Test Failures** - GitHub Actions fails UI tests due to workspace configuration requirements in headless Avalonia tests. Need to provide mock workspace or bypass requirement for test environments.

## Backlog

### Documentation
- Add XML docs to Core/Infrastructure public APIs

### Testing & Performance
- Validate query performance at 100k+ logs
- Add integration tests for UI flows

### Packaging
- Prepare Nix/Docker distribution
- Document runtime dependencies

### UI Polish
- Run history and quick-open for log viewers
- Accessibility improvements

### Future Ideas
- Console/CLI host using `IApplicationHost`
- Separate `Infra.Logging` package

---

## Recently Completed

### Code Review Fixes (2026-03-01)
- Replaced 51 error-swallowing catch blocks with proper Serilog logging
- Added `IDisposable` to `FileConverterViewModel` for event cleanup
- Refactored VM to use constructor injection (`IFileProcessingService`, `ISettingsService`)
- Removed debug statements from progress text
- Changed log channel from `DropOldest` to `Wait` policy with buffer-full warning
- Documented schema migration strategy

### Architecture Hardening
- Core is framework-agnostic (Serilog adapters in Infrastructure)
- Instance-based `WorkspaceRuntime` with bounded channel writer
- `IApplicationHost` for host-agnostic lifecycle
- DI-backed window factory and ViewModels
- 200+ tests with 95%+ coverage on Core/Infrastructure
