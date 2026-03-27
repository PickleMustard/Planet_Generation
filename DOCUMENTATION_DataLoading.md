# Data Loading System

## Overview
The Data Loading System provides a startup loading screen with progress visualization for database initialization. It replaces direct autoloading of databases with an asynchronous loading system that shows progress to the user.

## Components

### 1. DataLoading Scene (`Scenes/DataLoading.tscn`)
- **Root Node**: `Node` with `DataLoadingScene.cs` script
- **UI Structure**:
  - `Background`: ColorRect for background
  - `LoadingUI`: Control container with VBoxLayout
    - `TitleLabel`: "Loading Game Data"
    - `OverallProgressBar`: Shows combined progress of all databases
    - `StatusLabel`: Current status text
    - `DatabaseProgressContainer`: Container for individual database progress items
    - `ErrorPanel`: Hidden panel shown on loading errors
      - `ErrorMessageLabel`: Error description
      - `RetryButton`: Button to retry loading
      - `SkipButton`: Button to skip to main menu (with potential missing data)

### 2. DataLoadingScene Script (`Scripts/UtilityLibrary/DataLoading/DataLoadingScene.cs`)
- **Purpose**: Manages the loading process and UI updates
- **Key Methods**:
  - `_Ready()`: Initializes UI, load manager, and starts loading
  - `_Process()`: Updates overall progress bar and status
  - `InitializeLoadManager()`: Creates/gets DatabaseLoadManager instance
  - `RegisterDatabases()`: Registers ResourceDatabase and BuildingDatabase
  - `StartDatabaseLoading()`: Starts asynchronous loading via ThreadPooler
  - `TransitionToMainMenu()`: Changes scene to MainMenu.tscn
  - `ShowError()`: Displays error panel with retry/skip options

### 3. DatabaseProgressItem Template (`UI/DataLoading/DatabaseProgressItem.tscn`)
- **Structure**: Control with HBoxContainer containing:
  - `NameLabel`: Database name
  - `ProgressBar`: Individual database progress
  - `StatusLabel`: Current status (Waiting..., Loading..., Complete, Error)

### 4. DatabaseLoadManager (`Scripts/UtilityLibrary/DataLoading/DatabaseLoadManager.cs`)
- **Purpose**: Manages asynchronous loading of multiple databases
- **Features**:
  - Concurrent loading with configurable parallelism
  - Progress tracking for each database
  - Batch loading with completion tracking
  - Event system for load start/progress/completion

## Usage Flow

1. **Startup**: Game launches with DataLoading scene as main scene
2. **Initialization**: DataLoadingScene creates DatabaseLoadManager instance
3. **Registration**: ResourceDatabase and BuildingDatabase are registered
4. **Loading**: Databases load asynchronously via ThreadPooler
5. **Progress Updates**: UI shows individual and overall progress
6. **Completion**: When all databases load, transitions to MainMenu
7. **Error Handling**: Shows error panel with retry/skip options on failure

## Configuration

### Project Settings (`project.godot`)
- `run/main_scene`: Set to `"res://Scenes/DataLoading.tscn"`
- **Autoloads**: Database singletons (ResourceDatabase, BuildingDatabase) should NOT be autoloaded

### DatabaseLoadManager Settings
- `max_concurrent_loads`: Maximum databases to load simultaneously (default: 2)
- Configured via RuntimeSettings system

## Adding New Databases

To add a new loadable database:

1. **Implement Interface**: Class must implement `ILoadableDatabase`
2. **Add Registration**: Update `DataLoadingScene.RegisterDatabases()` to register the new database
3. **Update UI**: The system automatically creates progress items for registered databases

## Dependencies

- **DatabaseLoadManager**: Requires ThreadPooler for background loading
- **DataLoadingScene**: Requires DatabaseLoadManager, ResourceDatabase, BuildingDatabase
- **ResourceDatabase/BuildingDatabase**: Must implement ILoadableDatabase interface

## Testing

Run the game to verify:
1. DataLoading scene appears on startup
2. Progress bars update during loading
3. Successful transition to MainMenu
4. Error handling works (simulate load failure)

## Known Limitations

1. **Scene Transition**: Currently uses basic `ChangeSceneToFile()` - consider adding fade transitions
2. **Error Recovery**: Limited to retry or skip options
3. **Progress Accuracy**: Depends on individual database progress reporting

## Future Enhancements

1. **Animated Transitions**: Add fade in/out between scenes
2. **Loading Tips**: Display random tips during loading
3. **Background Image**: Replace solid color with themed background
4. **Sound Effects**: Add loading completion sound
5. **Progress Persistence**: Save loading state for faster subsequent launches