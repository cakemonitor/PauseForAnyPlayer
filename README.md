# Pause For Any Player

A Stardew Valley mod that pauses the game when any player is in a paused state, supporting both splitscreen and network multiplayer.

## Features

### Core Functionality
- **Universal Pause**: Game time stops when any player pauses (menu open, dialogue, etc.)
- **Splitscreen Support**: Works seamlessly with local splitscreen multiplayer
- **Network Multiplayer**: Syncs pause states across all connected players
- **Smart Host Control**: Only the host controls time in network games to prevent conflicts

### Energy Scaling
- **Configurable Energy Gains**: Adjust how much energy players gain from food/sleep
- **Default 25% Reduction**: Energy gains scaled to 75% by default (configurable)
- **Multiplayer Compatible**: Host controls energy scaling in network games

### Configuration
- **Generic Mod Config Menu Integration**: Easy in-game configuration
- **Energy Scale Factor**: Adjustable from 0.1x to 2.0x (default: 0.75x)
- **Host-Only Settings**: Configuration only available to host in network multiplayer

## Installation

1. Install [SMAPI](https://smapi.io/)
2. Download the latest release from [releases page]
3. Extract to your `Stardew Valley/Mods/` folder
4. Launch the game through SMAPI

## Dependencies

- **Required**: SMAPI 4.0.0+
- **Optional**: [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) for in-game configuration

## Configuration

### Via Generic Mod Config Menu (Recommended)
1. Install Generic Mod Config Menu
2. Open the game settings menu
3. Navigate to "Mod Options" → "Pause For Any Player"
4. Adjust the Energy Scale Factor slider (0.1x - 2.0x)

### Manual Configuration
Edit `config.json` in the mod folder:
```json
{
  "EnergyScaleFactor": 0.75
}
```

## How It Works

### Splitscreen Multiplayer
- Monitors all local players' pause states
- Automatically pauses game time when any player has menus open or is in dialogue

### Network Multiplayer
- Each client sends their pause state to the host
- Host controls game time based on all connected players' states
- Pause states are synchronized when players connect/disconnect

### Energy Scaling
- Tracks energy changes for all players
- Applies scaling factor only to energy gains (not losses)
- Prevents energy exploitation while maintaining balance

## Building

See [BUILD.md](BUILD.md) for detailed build instructions.

### Quick Build
```bash
# Debug build with auto-deploy
dotnet build

# Release build
dotnet build --configuration Release
```

## Compatibility

- **Stardew Valley**: 1.6+
- **SMAPI**: 4.0.0+
- **Platforms**: Windows, Mac, Linux
- **Multiplayer**: Full support for both splitscreen and network multiplayer

## Technical Details

- Uses SMAPI's `requestingTimePause` to detect player pause states
- Implements message passing for network multiplayer synchronization
- Caches game time interval to restore proper timing after pause
- Tracks per-player energy states to prevent scaling conflicts

## License

[Add your license here]

## Credits

- **Author**: cakemonitor
- **Version**: 1.0.0
- Built with [SMAPI](https://smapi.io/)