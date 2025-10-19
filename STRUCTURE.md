# FileProcessor Solution Structure

## 📁 **Improved Project Organization**

This solution follows .NET and Avalonia UI best practices for maintainable, scalable applications.

### **Solution Structure**
```
FileProcessor.sln                 # Solution file
├── FileProcessor.UI/             # 🎨 Main Avalonia UI Application
│   ├── Assets/                   # Images, fonts, and static resources
│   ├── Converters/              # XAML value converters
│   ├── Interfaces/              # UI-specific interfaces
│   ├── Models/                  # UI data models and DTOs
│   ├── Resources/               # Shared XAML resources and styles
│   ├── Services/                # UI services (themes, navigation, etc.)
│   ├── ViewModels/              # MVVM ViewModels
│   ├── Views/                   # XAML views and user controls
│   ├── App.axaml               # Application entry point
│   ├── MainWindow.axaml        # Main application window
│   └── Program.cs              # Application startup
├── FileProcessor.Core/          # 🔧 Business Logic Library
│   ├── Interfaces/             # Core business interfaces
│   ├── Logging/                # Logging contracts and abstractions
│   ├── Workspace/              # Workspace database abstractions
│   ├── FileProcessingService.cs # File processing operations
│   └── FileGenerationService.cs # File generation operations
├── FileProcessor.Infrastructure/ # 🏗️ Cross-Cutting Infrastructure
│   ├── Logging/                # Logging service implementations
│   └── Workspace/              # Workspace database implementation
├── LogViewer.UI/               # 🔍 Standalone Log Viewer Application
└── SampleFiles/                # 📄 Test files for development
```

## 🏗️ **Architecture Principles**

### **1. Separation of Concerns**
- **UI Layer** (`FileProcessor.UI`): Handles user interface, data binding, and user interactions
- **Business Layer** (`FileProcessor.Core`): Contains business logic, data processing, and domain operations
- **Tool Layer** (`FileProcessor.Generator`): Command-line utilities and batch operations

### **2. Clean Architecture**
- **UI Layer** (`FileProcessor.UI`): Handles user interface, data binding, and user interactions
- **Business Layer** (`FileProcessor.Core`): Contains business logic, data processing, and domain operations  
- **Infrastructure Layer** (`FileProcessor.Infrastructure`): Cross-cutting concerns like logging, configuration, external services, and workspace database

### **3. Dependency Injection Ready**
- All services implement interfaces for testability and flexibility
- Infrastructure services are shared between UI and CLI applications
- Services can be easily mocked for unit testing
- Ready for DI container integration (Microsoft.Extensions.DependencyInjection)

### **3. MVVM Pattern**
- **Models**: Data structures and business entities
- **ViewModels**: Presentation logic and data binding
- **Views**: XAML UI definitions with minimal code-behind

### **4. Consistent Naming**
- All projects follow `FileProcessor.*` naming convention
- Namespace alignment: `FileProcessor.UI.ViewModels`, `FileProcessor.Core.Interfaces`
- Clear, descriptive folder and file names

## 📦 **Key Components**

### **Interfaces**
- `IFileProcessingService`: Async file processing operations
- `IFileGenerationService`: Async file generation operations  
- `IThemeService`: Application theme management

### **Services**
- `FileProcessingService`: Implements file processing with async support
- `FileGenerationService`: Handles file creation and batch generation
- `ThemeService`: Manages light/dark theme switching

### **ViewModels**
- `MainWindowViewModel`: Main application state and navigation
- `FileProcessorViewModel`: File processing operations UI
- `FileGeneratorViewModel`: File generation operations UI
- `SettingsViewModel`: Application settings and preferences

### **Workspace Database**
- **Purpose**: SQLite-based workspace store for sessions, operations, items, and logs to enable fast querying and filtering of large log files (e.g., 30k+ entries).
- **Key Interfaces** (`FileProcessor.Core.Workspace`):
  - `IWorkspaceDb`: Core database operations (initialize, query logs, group counts)
  - `IRunStore`: Session and run management
  - `ILogStore`: Log entry operations
- **Schema** (`FileProcessor.Infrastructure.Workspace.WorkspaceSchema.sql`): SQLite DDL with tables for `sessions`, `operations`, `items`, and `log_entries`, including indexes for performance.
- **Implementation** (`FileProcessor.Infrastructure.Workspace`):
  - `SqliteWorkspaceDb`: SQLite database operations using Microsoft.Data.Sqlite with WAL mode for concurrent access.
  - `WorkspaceDbService`: Static facade for database lifecycle, session/run management.
- **Logging Integration** (`FileProcessor.Infrastructure.Logging`):
  - `WorkspaceSqliteSink`: Serilog sink that mirrors all log events to SQLite DB for queryable storage.
  - `WorkspaceRunStructuredLogger`: Structured logging to Serilog (with DB mirroring handled by sink).
  - Materialization: `WorkspaceDbService.MaterializeOperationLogsAsync` and `MaterializeSessionLogsAsync` export logs to JSONL for portability.

## 🎯 **Benefits of This Structure**

1. **✅ Maintainability**: Clear separation makes code easier to understand and modify
2. **✅ Testability**: Interface-based design enables comprehensive unit testing
3. **✅ Scalability**: Modular structure supports adding new features and services
4. **✅ Best Practices**: Follows established .NET and Avalonia UI conventions
5. **✅ Team Development**: Clear structure helps team members navigate the codebase

## 🚀 **Getting Started**

```bash
# Build the solution
dotnet build

# Run the UI application
dotnet run --project FileProcessor.UI

# Run the CLI generator
dotnet run --project FileProcessor.Generator
```

## 🔧 **Development Guidelines**

- Place new UI components in appropriate folders (`Views/`, `ViewModels/`, `Converters/`)
- Create interfaces for all services to maintain testability
- Use the `Resources/` folder for shared XAML styles and resources
- Follow async/await patterns for all I/O operations
- Maintain consistent namespace structure across projects
