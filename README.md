# Fileonia

This project is a .NET Avalonia application for processing files.

## Development Environment

This project uses Nix to manage the development environment. To get started, you'll need to have Nix installed.

Once you have Nix installed, you can enter the development environment by running the following command in the project's root directory:

```bash
nix-shell
```

This will install all the necessary dependencies and configure the environment for you.

## Running the Application

Once you're in the Nix shell, you can run the application with the following command:

```bash
dotnet run --project Fileonia.UI/Fileonia.UI.csproj
```
