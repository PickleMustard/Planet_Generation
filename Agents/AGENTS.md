# Agent Configuration for Delaunay Triangulation Map Generation

## Build Commands
- Build: `dotnet build`
- Run: `dotnet run`
- Clean: `dotnet clean`

## Code Style Guidelines
- Use C# naming conventions (PascalCase for classes, methods, properties; camelCase for variables)
- Use Godot's Vector3 and Mathf types for 3D math operations
- Prefer float over double for performance
- Use explicit typing over var
- Follow Godot's C# style guide for GDScript interop
- Handle errors with try/catch blocks and meaningful error messages
- Use Godot's GD.Print for debugging instead of Console.WriteLine
- Organize imports in alphabetical order
- Use regions to separate logical sections of code
- Include XML documentation for public methods and classes

## Testing
- No specific test framework detected in project files
- Manual testing through Godot editor recommended
- Use Godot's built-in debugger for runtime inspection

## Project Structure
- C# scripts in root directory
- Structure definitions in Structures/ folder
- Godot scene files (.tscn) for visualization
- Shader files (.gdshader) for rendering
