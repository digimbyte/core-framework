# Core Framework - Package Architecture

## Git URL Package Structure
This project is distributed as a Unity package via git URL (`com.digimbyte.core`). As such, it relies on Assembly Definition files (`.asmdef`) to expose APIs to consumers.

## Assembly Definitions
The package exposes the following assemblies:
- `Core.Registry` - Main registry system
- `Core.Registry.Editor` - Editor tooling for registry
- `Core.Data` - Data handling
- `Core.Data.Editor` - Editor tooling for data
- `Core.Signals` - Signal/event system
- `Core.Utility` - Utility classes (StringCompression, etc.)
- `Nova` - GUI system
- `Nova.Editor` - GUI editor tooling
- `Animator` / `Animator.Editor` - Animation utilities

Only code within these `.asmdef` boundaries is part of the public API.
