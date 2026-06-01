#!/usr/bin/env python3
"""
Migration script to split the single ResourceDefinition.yaml file into
category-based YAML files.

This script:
1. Reads the existing ResourceDefinition.yaml file
2. Groups resources by their resource_type field
3. Creates category files (e.g., ore.yaml, raw_material.yaml)
4. Removes the resource_type field from individual resources
5. Preserves all other fields
6. Creates a backup of the original file
"""

import os
import yaml
import shutil
from pathlib import Path
from datetime import datetime

def load_yaml_file(filepath):
    """Load YAML file safely."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            return yaml.safe_load(f)
    except yaml.YAMLError as e:
        print(f"Error parsing YAML file {filepath}: {e}")
        return None
    except Exception as e:
        print(f"Error reading file {filepath}: {e}")
        return None

def save_yaml_file(filepath, data):
    """Save data to YAML file with proper formatting."""
    try:
        # Create directory if it doesn't exist
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
        
        with open(filepath, 'w', encoding='utf-8') as f:
            yaml.dump(data, f, default_flow_style=False, sort_keys=False, width=100)
        print(f"Saved {filepath}")
        return True
    except Exception as e:
        print(f"Error saving file {filepath}: {e}")
        return False

def create_backup(original_path):
    """Create a timestamped backup of the original file."""
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = f"{original_path}.backup_{timestamp}"
    
    try:
        shutil.copy2(original_path, backup_path)
        print(f"Created backup at: {backup_path}")
        return backup_path
    except Exception as e:
        print(f"Failed to create backup: {e}")
        return None

def migrate_resources():
    """Main migration function."""
    
    # Paths
    project_root = Path("/home/picklemustard/Programming/Game-Development/Planet_Generation")
    original_file = project_root / "Configuration" / "ResourceDefinition" / "ResourceDefinition.yaml"
    categories_dir = project_root / "Configuration" / "ResourceDefinition" / "categories"
    
    print(f"Original file: {original_file}")
    print(f"Categories directory: {categories_dir}")
    
    # Check if files exist
    if not original_file.exists():
        print(f"Error: Original file not found at {original_file}")
        return False
    
    # Create backup
    print("\nCreating backup of original file...")
    backup_path = create_backup(original_file)
    if not backup_path:
        print("Warning: Could not create backup, but proceeding anyway...")
    
    # Load original data
    print("\nLoading original YAML file...")
    data = load_yaml_file(original_file)
    if not data or 'resources' not in data:
        print("Error: Invalid YAML structure or missing 'resources' key")
        return False
    
    resources = data['resources']
    print(f"Found {len(resources)} resources")
    
    # Group resources by resource_type
    print("\nGrouping resources by resource_type...")
    grouped = {}
    
    for resource in resources:
        if 'resource_type' not in resource:
            print(f"Warning: Resource missing resource_type field: {resource.get('id_name', 'unknown')}")
            continue
        
        resource_type = resource['resource_type']
        
        # Create a copy without the resource_type field
        resource_copy = resource.copy()
        del resource_copy['resource_type']
        
        if resource_type not in grouped:
            grouped[resource_type] = []
        
        grouped[resource_type].append(resource_copy)
    
    # Print summary
    print("\nResource type distribution:")
    for resource_type, items in grouped.items():
        print(f"  {resource_type}: {len(items)} resources")
    
    # Save to category files
    print("\nSaving category files...")
    
    # Define the expected category files based on analysis
    expected_categories = {
        'ore': 'ore.yaml',
        'raw_material': 'raw_material.yaml',
        'fuel': 'fuel.yaml',
        'food': 'food.yaml',
        'electronic': 'electronic.yaml',
        'industrial': 'industrial.yaml',
        'construction': 'construction.yaml'
    }
    
    # Ensure categories directory exists
    os.makedirs(categories_dir, exist_ok=True)
    
    # Save each category
    saved_files = []
    for resource_type, filename in expected_categories.items():
        if resource_type in grouped:
            category_data = {'resources': grouped[resource_type]}
            filepath = categories_dir / filename
            
            if save_yaml_file(filepath, category_data):
                saved_files.append(filepath)
                
                # Print example of what was saved
                if grouped[resource_type]:
                    first_resource = grouped[resource_type][0]
                    print(f"  {filename}: {len(grouped[resource_type])} resources (e.g., {first_resource.get('id_name', 'unknown')})")
            else:
                print(f"  Error saving {filename}")
        else:
            print(f"  Warning: No resources found for type '{resource_type}', creating empty file")
            # Create empty file for consistency
            category_data = {'resources': []}
            filepath = categories_dir / filename
            if save_yaml_file(filepath, category_data):
                saved_files.append(filepath)
    
    # Check for any resource types not in expected categories
    unexpected_types = set(grouped.keys()) - set(expected_categories.keys())
    if unexpected_types:
        print(f"\nWarning: Found unexpected resource types: {unexpected_types}")
        print("These will need to be handled separately.")
    
    print(f"\nMigration completed:")
    print(f"  - Created {len(saved_files)} category files")
    print(f"  - Processed {len(resources)} total resources")
    print(f"  - Backup saved to: {backup_path or 'No backup created'}")
    
    # Print next steps
    print("\nNext steps:")
    print("1. Verify the generated files in: Configuration/ResourceDefinition/categories/")
    print("2. Update the code to load from category files")
    print("3. Remove the old ResourceDefinition.yaml file after testing")
    
    return True

def verify_migration():
    """Verify the migration was successful."""
    print("\nVerifying migration...")
    
    project_root = Path("/home/picklemustard/Programming/Game-Development/Planet_Generation")
    categories_dir = project_root / "Configuration" / "ResourceDefinition" / "categories"
    
    if not categories_dir.exists():
        print("Error: Categories directory not found")
        return False
    
    category_files = list(categories_dir.glob("*.yaml"))
    print(f"Found {len(category_files)} category files:")
    
    total_resources = 0
    for filepath in sorted(category_files):
        data = load_yaml_file(filepath)
        if data and 'resources' in data:
            count = len(data['resources'])
            total_resources += count
            print(f"  {filepath.name}: {count} resources")
        else:
            print(f"  {filepath.name}: ERROR - invalid format")
    
    print(f"Total resources across all files: {total_resources}")
    
    return len(category_files) > 0

if __name__ == "__main__":
    print("=" * 60)
    print("Resource Definition Migration Script")
    print("=" * 60)
    
    try:
        if migrate_resources():
            print("\nMigration completed successfully!")
            verify_migration()
        else:
            print("\nMigration failed!")
    except Exception as e:
        print(f"\nUnexpected error during migration: {e}")
        import traceback
        traceback.print_exc()