#!/usr/bin/env bash

# Script to run the FileProcessor application with proper Nix environment
# This ensures all native dependencies are available

echo "Starting FileProcessor application..."
echo "Using nix-shell to provide Avalonia dependencies..."

# Enter nix-shell and run the application
nix-shell --run "dotnet run --project FileProcessor.UI"
