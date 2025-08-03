# A comprehensive shell for .NET Avalonia development on NixOS
{
  pkgs ? import <nixpkgs> {},
}: let
  # A comprehensive list of native libraries required for a full-featured Avalonia application.
  avalonia-deps = with pkgs; [
    # --- Core Skia / Font Dependencies ---
    fontconfig
    freetype
    libjpeg_turbo
    libpng
    zlib

    # --- X11 Windowing System Dependencies ---
    xorg.libX11
    xorg.libXcursor
    xorg.libXrandr
    xorg.libXi
    xorg.libICE
    xorg.libSM
    xorg.libXext
    xorg.libXfixes
    xorg.libXrender
    xorg.libXinerama

    # --- GTK3 Backend & Desktop Integration Dependencies ---
    gtk3
    gdk-pixbuf
    glib
    pango
    cairo
    at-spi2-atk
    dbus
  ];

in
  pkgs.mkShell {
    # Add the .NET SDK and all native libraries to the environment's packages.
    packages = [pkgs.dotnet-sdk_8] ++ avalonia-deps;

    # This hook runs when you enter the shell. It sets the crucial
    # environment variable that tells the .NET runtime where to find all the .so files.
    shellHook = ''
      export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath avalonia-deps}:$LD_LIBRARY_PATH"
    '';
  }
