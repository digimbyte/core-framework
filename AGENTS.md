# Core Framework maintenance

## Odin package importer path patch

Odin Inspector and Serializer 4.0.2.4 contains a build-preprocessing bug when
installed through Unity Package Manager. Its assembly import automation passes
physical `Library/PackageCache/...` paths to `AssetImporter.GetAtPath`, which
requires an asset path such as `Packages/com.digimbyte.core/...`. This causes
`InvalidOperationException: Failed to get PluginImporter` for the runtime
`Sirenix.Serialization.dll` variants before the player build begins.

The root `Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll` is patched in
`Sirenix.Serialization.Utilities.Editor.AssemblyImportSettingsUtilities`:

- The four-argument `SetAssemblyImportSettings` overload calls the private
  `ResolvePackageAssetPath` helper immediately before `AssetImporter.GetAtPath`.
- The helper maps physical paths through `PackageInfo.GetAllRegisteredPackages`,
  matching a package's `resolvedPath` with a directory boundary and returning its
  `assetPath` plus the relative file path. It normalizes separators and respects
  platform path case sensitivity. Paths outside registered packages are unchanged.
- The original file-existence check, assembly selection, platform/editor flags,
  and reimport behavior remain unchanged. The matching PDB is rewritten with the
  DLL. Runtime variant DLLs in `NoEditor` and `NoEmitAndNoEditor` are not patched.

When replacing or upgrading Odin, inspect the incoming implementation before
overwriting this fix. If the vendor still supplies physical package-cache paths
to the importer lookup, reapply the narrow path conversion and regenerate matching
symbols. Do not disable Odin's import automation, change runtime variant settings,
or edit a consumer project's package cache to hide this failure. If the vendor
fixes the lookup, use the vendor implementation without duplicating the patch.

Validate DLL/PDB consistency and package-path resolution, then verify a player
build from a consuming UPM project. Distinguish path/assembly checks from a
successful full player build when reporting results.
