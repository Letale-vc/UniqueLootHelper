# UniqueLootHelper

Highlights Labels of specified Unique Items via their Icon Model File.

![2021-11-23 15_49_38-Clipboard](https://user-images.githubusercontent.com/36637378/143047478-4bf8aa28-443c-469f-b763-07ab9ad5411b.png)

## 🎯 Features

- **Visual Highlighting** - Highlight unique items on the ground by their art path (icon)
- **Preset Item Selection** - Choose from 1000+ predefined unique items with searchable list
- **Custom Items** - Manual entry for custom art paths still supported
- **Sound Notifications** - Play custom sounds when valuable items are found
- **Statistics Tracking** - Track how many times each item has been found
- **Map & World Lines** - Draw lines from player to items on map and in world
- **Label Customization** - Customize labels for unidentified items
- **Import/Export** - Share your configurations via base64-encoded strings
- **Corruption Filter** - Option to hide/show corrupted items


## 📦 Installation

1. Clone or download this repository into your ExileCore `Plugins/Source` folder
2. Rebuild the solution or let ExileCore compile it automatically
3. Enable the plugin in ExileCore settings

## 🔧 Configuration

### Adding Unique Items

#### Using Preset Items (Recommended)

1. Open plugin settings
2. Check "Use Preset Item" checkbox
3. Type in the search box to find your item (e.g., "Headhunter")
4. Select the item from the list
5. (Optional) Customize the label
6. Configure display options
7. Click "Add Unique"

#### Using Custom Art Path

1. Hover over an item in-game
2. Press `F7` to copy the art path to clipboard
3. Open plugin settings
4. Leave "Use Preset Item" unchecked
5. Paste the art path in "Unique art path" field
6. Set a label name
7. Configure display options:
    - Draw line on map
    - Draw line on world
    - Draw outline
    - Draw label name
    - Draw label in box
    - Draw is corrupted
    - Play valuable sound
8. Click "Add Unique"

### Regenerating Unique Items List

Click "Regenerate Unique Items List" button to update the predefined items list. The plugin will run `UniqueArtGenerate-win-x64.exe` (included with the plugin) to generate an updated list.

### Sound Notifications

Place `.wav` files in the plugin's config directory:

- `default.wav` - Default sound for all items
- `{ItemName}.wav` - Custom sound for specific item (using the label name)

Example:

```
Headhunter.wav - Will play when Headhunter is found
```

### Import/Export

**Export:**

1. Click "Export" button in settings
2. Configuration is copied to clipboard as base64 string

**Import:**

1. Paste base64 string into "Import/export" field
2. Click "Import" button
3. Configurations will be merged with existing ones
