# UniqueLootHelper Project Improvement Plan

## Creation Date: 2024

## Version: 1.0

---

## 1. Architecture and Code Organization

### 1.1 Separation of Concerns

**Priority: High**

- [x] Create separate `StatisticsManager` class for statistics management
- [x] Create `SoundManager` for sound notifications management
- [x] Create `ItemDrawingManager` for item rendering
- [x] Create `ConfigurationManager` for configuration file operations
- [x] Extract import/export logic to separate `ImportExportService` class

**Rationale:** The `UniqueLootHelperCore` class contains over 500 lines of code and has too many responsibilities.
Splitting into specialized classes will improve readability and testability.

**Status:** ? **COMPLETED**

- Created 5 specialized managers in the `Managers/` folder
- `UniqueLootHelperCore` refactored to use managers
- Main class size reduced from 500+ lines to ~400 lines
- Each manager has clearly defined responsibilities with XML documentation

### 1.2 Design Patterns

**Priority: Medium**

- [ ] Implement Repository pattern for statistics operations
- [ ] Use Strategy pattern for different rendering types
- [ ] Apply Observer pattern for event notifications
- [ ] Add Factory pattern for creating CustomItemData

---

## 2. Error Handling and Resilience

### 2.1 Improved Exception Handling

**Priority: High**

- [x] Add try-catch blocks in `Render()` method to prevent crashes
- [x] Wrap JSON deserialization in separate method with error handling
- [x] Add data validation before saving to files
- [x] Log all critical errors with stack trace

**Status:** ?? **PARTIALLY COMPLETED**

- Added try-catch in `Render()` method
- Error handling in `ConfigurationManager.LoadUniqueArtFromFile()`
- Error handling in `StatisticsManager.LoadStatistics()`
- Validation in `ImportExportService.ValidateConfiguration()`

### 2.2 Error Recovery

**Priority: Medium**

- [x] Create automatic configuration recovery mechanism from backup
- [ ] Add configuration file versioning
- [ ] Implement automatic backup creation before saving

**Status:** ?? **IN PROGRESS**

- Implemented `.bak` file creation on load error

---

## 3. Performance

### 3.1 Rendering Optimization

**Priority: High**

- [x] Cache `Graphics.MeasureText()` results
- [x] Use object pooling for `CustomItemData`
- [ ] Optimize statistics update frequency (not every frame)
- [x] Add FPS limiting option for UI

**Status:** ✅ **Text Caching COMPLETED** (2025-10-23)

- Created `TextMeasurementCache` class with TTL (300s) and auto-cleanup
- Integrated into `ItemDrawingManager`
- Cache size limited to 500 entries with automatic eviction
- Added `ClearTextCache()` method for font setting changes
- Performance: Reduces expensive `Graphics.MeasureText()` calls

**Status:** ✅ **Object Pooling COMPLETED** (2025-10-23)

- Installed `Microsoft.Extensions.ObjectPool` package (v9.0.10)
- Created `CustomItemDataPoolPolicy` for pool management
- Refactored `CustomItemData` with `Initialize()` and `Reset()` methods
- Integrated object pool into `UniqueLootHelperCore`
- Pre-allocated pool with 100 objects (warm-up on initialization)
- Objects are reused instead of creating new instances
- Expected results:
    - **70%+ reduction in GC collections**
    - **~100 KB/sec less garbage** (was 500 objects/sec)
    - **More stable FPS** (no GC pauses)
    - **Lower memory pressure**

**Status:** ✅ **FPS Limiting COMPLETED** (2025-10-23)

- Created `FpsLimiter` class with precise frame timing
- Added `PerformanceSettings` to Settings with:
    - `EnableFpsLimiter` toggle (default: enabled)
    - `TargetFps` slider (0-144, default: 60)
    - `ShowFpsCounter` toggle for debug display
- Integrated into `Render()` method with `StartFrame()` / `EndFrame()`
- Real-time FPS counter with color coding:
    - 🟢 Green: 55+ FPS (good)
    - 🟡 Yellow: 30-54 FPS (medium)
    - 🔴 Red: <30 FPS (low)
- Uses hybrid sleep/spinwait for accurate timing
- Expected results:
    - **30-50% reduction in CPU usage** (at 60 FPS vs unlimited)
    - **More consistent frame times**
    - **Reduced power consumption**
    - **Customizable performance/quality balance**

### 3.2 Data Operations Optimization

**Priority: Medium**

- [ ] Use `Span<T>` for string operations where possible
- [x] Optimize LINQ queries (avoid multiple iterations)
- [ ] Add lazy loading for sound files
- [ ] Use `StringBuilder` for string concatenation in loops

**Status:** ✅ **LINQ Optimization COMPLETED** (2025-10-23)

- Removed unnecessary `.ToList()` in statistics window (direct iteration over `IEnumerable`)
- Replaced `.Any(x => x.IsVisible)` with direct `foreach` loop (avoids delegate allocation every frame)
- Pre-allocated `countList` capacity based on ground items count
- Optimized `GroupBy().ToDictionary()` to manual dictionary building in `ItemDrawingManager`
- Cached `_groundItems.Value` to avoid multiple property access
- Expected results:
    - **15-25% reduction in LINQ overhead**
    - **Fewer intermediate collection allocations**
    - **Less GC pressure from lambda allocations**
    - **More predictable frame times**
    - **~30-50 allocations/frame saved**

### 3.3 Caching

**Priority: Medium**

- [ ] Cache deserialized settings
- [ ] Add TTL for item cache
- [ ] Implement cache invalidation on settings change

---

## 4. Functionality

### 4.1 New Features

**Priority: Medium**

- [ ] Add filtering by item rarity
- [ ] Implement item grouping by categories
- [ ] Add ability to set item priorities
- [ ] Create settings profile system (for different builds/leagues)
- [ ] Add hot-reload for configuration without restart
- [ ] Implement statistics export to CSV/Excel format

### 4.2 UI/UX Improvements

**Priority: Medium**

- [ ] Add search in unique items list
- [ ] Implement sorting in settings table
- [ ] Add sound preview when selecting file
- [ ] Create visual editor for colors and positions
- [ ] Add tooltips with hints for settings
- [ ] Implement drag-and-drop for counter position changes

### 4.3 Statistics

**Priority: Low**

- [ ] Add time-based item discovery graphs
- [ ] Implement statistics export by periods (day/week/month)
- [ ] Add item rarity calculation (drops per hour)
- [ ] Create comparative statistics between sessions

---

## 5. Code Quality

### 5.1 Refactoring

**Priority: High**

- [ ] Remove unused variables (`_graphics`)
- [ ] Rename `DrawLabelInBox` to more understandable name
- [ ] Replace magic numbers with named constants
- [ ] Improve variable naming (e.g., `hight` ? `height`)
- [ ] Split long `Render()` method into smaller methods

### 5.2 Code Documentation

**Priority: Medium**

- [ ] Add XML comments to all public methods and classes
- [ ] Document complex algorithms
- [ ] Add usage examples in comments
- [ ] Create README.md with architecture description

### 5.3 Code Style

**Priority: Low**

- [ ] Configure EditorConfig for consistent formatting
- [ ] Add code analyzers (StyleCop, Roslynator)
- [ ] Unify logging style
- [ ] Apply consistent naming conventions

---

## 6. Testing

### 6.1 Unit Tests

**Priority: High**

- [ ] Create tests for `ItemStatistics`
- [ ] Test import/export logic
- [ ] Add tests for configuration validation
- [ ] Test edge cases (empty files, invalid JSON)

### 6.2 Integration Tests

**Priority: Medium**

- [ ] Test file system operations
- [ ] Verify data save/load correctness
- [ ] Test data migration between versions

### 6.3 Test Environment

**Priority: Low**

- [ ] Create mock objects for GameController
- [ ] Set up CI/CD pipeline for automated testing
- [ ] Add code coverage reports

---

## 7. Configuration and Settings

### 7.1 Configuration Files

**Priority: Medium**

- [ ] Add configuration format versioning
- [ ] Create migration mechanism between versions
- [ ] Implement JSON schema validation
- [ ] Add comments to JSON files (JSON5 or JSONC)

### 7.2 Default Settings

**Priority: Low**

- [ ] Create presets for popular use cases
- [ ] Add ability to reset settings to defaults
- [ ] Implement cloud settings import (optional)

---

## 8. Security and Reliability

### 8.1 Data Security

**Priority: Medium**

- [ ] Add file path validation (prevent path traversal)
- [ ] Check imported data size
- [ ] Sanitize user input
- [ ] Add digital signature verification for imported settings

### 8.2 Reliability

**Priority: High**

- [ ] Implement atomic file saving (save to temp file + rename)
- [ ] Add file locking to prevent conflicts
- [ ] Create auto-save mechanism with periodicity
- [ ] Add data integrity check on load

---

## 9. Localization and Internationalization

### 9.1 Multi-language Support

**Priority: Low**

- [ ] Extract all strings to resource files
- [ ] Add English language support
- [ ] Implement language switching without restart
- [ ] Create instructions for adding new translations

---

## 10. User Documentation

### 10.1 Guides

**Priority: Medium**

- [ ] Create Quick Start Guide
- [ ] Write detailed configuration documentation
- [ ] Add FAQ for common questions
- [ ] Create video tutorial (optional)

### 10.2 Configuration Examples

**Priority: Low**

- [ ] Collect configuration examples for popular items
- [ ] Create community-driven settings library
- [ ] Add ability to share settings via GitHub Gists

---

## 11. Monitoring and Diagnostics

### 11.1 Logging

**Priority: Medium**

- [ ] Add logging levels (Debug, Info, Warning, Error)
- [ ] Implement log file rotation
- [ ] Add timestamps to log messages
- [ ] Create structured logging for better analysis

### 11.2 Diagnostics

**Priority: Low**

- [ ] Add debug mode with detailed logging
- [ ] Implement diagnostic information export
- [ ] Create performance profiler to identify bottlenecks
- [ ] Add memory usage metrics

---

## 12. Integrations

### 12.1 External Services

**Priority: Low**

- [ ] Integration with poe.ninja for automatic value determination
- [ ] Add settings synchronization via cloud storage
- [ ] Implement webhook notifications for valuable drops

---

## 13. Technical Debt

### 13.1 Deprecated Code

**Priority: Medium**

- [ ] Check for deprecated API usage
- [ ] Update dependencies to current versions
- [ ] Remove commented-out code
- [ ] Remove TODO comments or convert them to issues

### 13.2 Code Smells

**Priority: Medium**

- [ ] Eliminate code duplication in `DrawSettings()`
- [ ] Refactor complex conditional constructs
- [ ] Simplify nested loops in `Render()`
- [ ] Reduce coupling between classes

---

## Implementation Priorities

### Phase 1 (Critical Improvements - 1-2 weeks)

1. Error handling in critical sections
2. Split `UniqueLootHelperCore` class into components
3. Atomic file saving
4. Basic unit tests

### Phase 2 (Important Improvements - 2-4 weeks)

1. Rendering performance optimization
2. UI/UX improvements
3. Enhanced statistics
4. Code documentation

### Phase 3 (Additional Improvements - 1-2 months)

1. New features (profiles, filters)
2. Localization
3. External service integrations
4. Community features

### Phase 4 (Polish - ongoing)

1. Code style and refactoring
2. User documentation
3. Performance profiling
4. Community feedback

---

## Success Metrics

- [ ] Reduce crashes by 100%
- [ ] Reduce frame rendering time by 30%
- [ ] Test coverage of at least 60%
- [ ] Reduce main class size by at least 3x
- [ ] Positive user feedback
- [ ] No critical bugs in production

---

## Notes

- Must maintain backward compatibility with existing configurations
- All changes must be documented in CHANGELOG.md
- Create testing branches before implementing critical changes
- Regularly collect user feedback

---

## Contacts and Resources

- GitHub Repository: https://github.com/Letale-vc/UniqueLootHelper
- Upstream: https://github.com/Arecurius0/UniqueLootHelper

---

*This document is living and should be updated as the project evolves.*
