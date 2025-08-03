# FileProcessor - Modular .NET 8 Avalonia Application

A well-structured example of a modular .NET 8 application using Avalonia UI, demonstrating best practices for cross-platform desktop development.

## 🎯 **Project Purpose**

This project serves as a **reference implementation** showcasing:

- ✅ **Clean Architecture** with proper separation of concerns
- ✅ **MVVM Pattern** implementation with CommunityToolkit.Mvvm
- ✅ **Interface-based Design** for testability and maintainability  
- ✅ **Cross-platform UI** with Avalonia 11.x
- ✅ **Modern .NET 8** features and patterns
- ✅ **Modular Project Structure** following industry standards

## 🏗️ **Architecture Overview**

```
├── FileProcessor.UI/        # 🎨 Avalonia UI Application (Presentation Layer)
├── FileProcessor.Core/      # 🔧 Business Logic Library (Domain Layer)  
├── FileProcessor.Generator/ # 🛠️ CLI Tool (Utility Layer)
└── SampleFiles/            # 📄 Test Data
```

## 🚀 **Getting Started**

### **Prerequisites**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Any IDE supporting .NET (Visual Studio, VS Code, JetBrains Rider)

### **Building & Running**

```bash
# Clone and navigate to the project
git clone <your-repo-url>
cd FileProcessor

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the UI application
dotnet run --project FileProcessor.UI

# Run the CLI generator tool
dotnet run --project FileProcessor.Generator
```

### **Development Environment (Optional)**

For **Nix users**, a development shell is provided for consistent environment setup:

```bash
nix-shell  # Provides .NET SDK and development tools
```

*Note: Nix is optional and used for testing reproducible builds. The project works on any system with .NET 8 SDK installed.*

## 🎨 **Features Demonstrated**

### **UI Features**
- 🌓 **Dynamic Theme Switching** (Light/Dark mode)
- 🧭 **Navigation System** with visual selection indicators
- 📁 **File Processing** with async operations
- 🎛️ **Settings Management** with data binding
- 🎨 **Material Design Icons** integration

### **Architecture Features**
- 🔌 **Dependency Injection Ready** with interface abstractions
- ⚡ **Async/Await Patterns** for responsive UI
- 🧪 **Testable Design** with service interfaces
- 📦 **Modular Services** for easy extension
- 🎯 **MVVM Implementation** with proper data binding

## 📁 **Project Structure**

### **FileProcessor.UI** (Presentation Layer)
```
├── Interfaces/     # UI service contracts
├── ViewModels/     # MVVM presentation logic
├── Views/          # XAML user interfaces  
├── Services/       # UI-specific services (themes, etc.)
├── Converters/     # XAML value converters
├── Resources/      # Shared styles and resources
└── Assets/         # Images, fonts, static files
```

### **FileProcessor.Core** (Business Layer)
```
├── Interfaces/     # Business logic contracts
├── Services/       # Core business services
└── Models/         # Domain entities (if needed)
```

### **FileProcessor.Generator** (Utility Layer)
```
└── CLI tool for batch file operations
```

## 🛠️ **Technologies Used**

- **[.NET 8](https://dotnet.microsoft.com/)** - Modern cross-platform framework
- **[Avalonia UI 11.x](https://avaloniaui.net/)** - Cross-platform XAML-based UI framework
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/MVVM-Toolkit)** - MVVM helpers and source generators
- **[Material.Icons.Avalonia](https://github.com/AvaloniaUtils/Material.Icons.Avalonia)** - Material Design icons

## 📚 **Learning Resources**

This project demonstrates patterns from:
- [Clean Architecture Principles](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MVVM Pattern Documentation](https://docs.microsoft.com/en-us/dotnet/architecture/maui/mvvm)
- [Avalonia UI Documentation](https://docs.avaloniaui.net/)
- [.NET Application Architecture Guides](https://dotnet.microsoft.com/learn/dotnet/architecture-guides)

## 🤝 **Contributing**

This is a reference project. Feel free to:
- 🍴 **Fork** for your own projects
- 📖 **Study** the patterns and structure  
- 💡 **Suggest improvements** via issues
- 📚 **Use as learning material** for .NET/Avalonia development

## 📄 **License**

This project is provided as-is for educational and reference purposes.

---

*Built with ❤️ to demonstrate modern .NET desktop application development*
