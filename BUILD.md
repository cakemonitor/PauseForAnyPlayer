# Build Instructions

## Quick Start

```bash
# Debug build (development)
dotnet build

# Release build (distribution)
dotnet build --configuration Release
```

Both commands automatically:
- Compile the mod
- Deploy to Stardew Valley Mods folder
- Create release zip in `bin/[Debug|Release]/net6.0/`

## Build Configuration

Configure ModBuildConfig in `.csproj`:

```xml
<PropertyGroup>
  <!-- Auto-deploy to game Mods folder -->
  <EnableModDeploy>true</EnableModDeploy>
  
  <!-- Create release zip -->
  <EnableModZip>true</EnableModZip>
  
  <!-- Custom mod folder name (default: project name) -->
  <ModFolderName>PauseForAnySplitscreenPlayer</ModFolderName>
  
  <!-- Custom game path (empty = auto-detect) -->
  <GameModsDir></GameModsDir>
</PropertyGroup>
```

## Common Options

- Set `<EnableModDeploy>false</EnableModDeploy>` to only build (no auto-deploy)
- Set `<EnableModZip>false</EnableModZip>` to skip zip creation
- Use `<GameModsDir>/custom/path</GameModsDir>` if auto-detection fails

## Clean Build

```bash
dotnet clean
```

Removes build outputs but leaves deployed mod in game folder.