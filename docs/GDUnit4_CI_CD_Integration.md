# GDUnit4 CI/CD Integration Guide for Godot 4.x with C#/.NET

**Research Date:** March 2026  
**Target Versions:** GDUnit4 v6.1.x+ for Godot 4.6 with C# and .NET 8.0  
**Sources:** 
- https://github.com/godot-gdunit-labs/gdUnit4
- https://godot-gdunit-labs.github.io/gdUnit4/latest/
- https://github.com/marketplace/actions/gdunit4-test-runner-action

---

## Table of Contents

1. [Command Line Tool](#1-command-line-tool)
2. [Test Report Generation](#2-test-report-generation)
3. [Best Practices for CI/Headless Environments](#3-best-practices-for-ciheadless-environments)
4. [Exit Codes and Pass/Fail Detection](#4-exit-codes-and-passfail-detection)
5. [Jenkins Integration](#5-jenkins-integration)
6. [GitHub Actions Integration](#6-github-actions-integration)
7. [Badge Generation](#7-badge-generation)
8. [C#/.NET Specific Configuration](#8-cnet-specific-configuration)

---

## 1. Command Line Tool

### Location
```
res://addons/gdUnit4/bin/GdUnitCmdTool.gd
```

### Helper Scripts
- **Linux/macOS:** `addons/gdUnit4/runtest.sh`
- **Windows:** `addons/gdUnit4/runtest.cmd`

### Basic Options

```bash
# Show help
./addons/gdUnit4/runtest.sh -help

# Show advanced options
./addons/gdUnit4/runtest.sh --help-advanced
```

### Command Reference

| Option | Description |
|--------|-------------|
| `-help` | Shows help message |
| `--help-advanced` | Shows advanced options |
| `-a, --add <directory\|path>` | Adds test suite or directory to execution pipeline |
| `-i, --ignore <name\|name:test>` | Adds test suite or test case to ignore list |
| `-c, --continue` | Continue running after first failure (instead of fail-fast) |
| `-conf, --config [file.cfg]` | Run tests by configuration file (default: GdUnitRunner.cfg) |

### Advanced Options

| Option | Description |
|--------|-------------|
| `-rd, --report-directory <dir>` | Output directory for reports (default: res://reports/) |
| `-rc, --report-count <count>` | Number of reports to keep (default: 20) |

### Usage Examples

```bash
# Run all tests in a directory
./addons/gdUnit4/runtest.sh -a res://tests

# Run multiple directories
./addons/gdUnit4/runtest.sh -a res://tests/foo -a res://tests/bar

# Run tests with ignore patterns
./addons/gdUnit4/runtest.sh -a res://tests -i ClassATest -i ClassBTest:test_abc

# Run with custom report directory
./addons/gdUnit4/runtest.sh -a res://tests -rd ./test-reports

# Run with continue-on-failure
./addons/gdUnit4/runtest.sh -a res://tests -c

# Run from previous configuration
./addons/gdUnit4/runtest.sh -conf
```

### Environment Setup

**Linux/macOS:**
```bash
export GODOT_BIN=/Applications/Godot.app/Contents/MacOS/Godot
chmod +x ./addons/gdUnit4/runtest.sh
```

**Windows (PowerShell/CMD):**
```cmd
setx GODOT_BIN D:\develop\Godot.exe
REG ADD HKCU\CONSOLE /f /v VirtualTerminalLevel /t REG_DWORD /d 1
```

---

## 2. Test Report Generation

GDUnit4 automatically generates two types of reports:

### JUnit XML Report
- **Location:** `reports/results.xml` (or `reports/report_<n>/results.xml`)
- **Format:** Standard JUnit XML format
- **Compatibility:** Jenkins, GitLab CI, GitHub Actions, and most CI systems
- **IBM JUnit XML Format Reference:** https://www.ibm.com/docs/en/developer-for-zos/14.1.0?topic=formats-junit-xml-format

### HTML Report
- **Location:** `reports/index.html`
- **Features:**
  - Modern, responsive web interface
  - Sortable by path or test suite
  - Detailed failure reports with stack traces
  - Logging integration
  - Test history trends

### Report Structure
```
reports/
├── index.html              # Main HTML report entry point
├── report_1/
│   ├── results.xml         # JUnit XML report
│   └── ...
├── report_2/
│   └── ...
└── ...
```

### Customizing Report Output

```bash
# Custom report directory
./addons/gdUnit4/runtest.sh -a res://tests -rd ./ci-reports

# Keep more reports (default is 20)
./addons/gdUnit4/runtest.sh -a res://tests -rc 50
```

---

## 3. Best Practices for CI/Headless Environments

### Running in Headless Mode

GDUnit4 runs tests in headless mode automatically when executed via command line:

```bash
# Godot headless mode with GDUnit4
godot --headless --path . -s addons/gdUnit4/bin/GdUnitCmdTool.gd -a res://tests
```

### Recommended CI Settings

1. **Use Specific Godot Versions** - Don't use "latest" in production CI
2. **Set Appropriate Timeouts** - Large test suites need more time
3. **Enable Continue Mode for Coverage** - Use `-c` to run all tests even if some fail
4. **Archive Reports** - Always save test reports as artifacts
5. **Use Retries for Flaky Tests** - Configure retry count for unreliable tests

### Performance Optimization

```yaml
# Example CI optimization
- Use caching for Godot binaries
- Cache .NET packages for C# projects
- Split large test suites across parallel jobs
- Use appropriate timeout values
```

### Pre-build Steps

```bash
# For C# projects, ensure proper build
dotnet build

# Then run tests
./addons/gdUnit4/runtest.sh -a res://tests
```

---

## 4. Exit Codes and Pass/Fail Detection

### Return Codes

| Code | Meaning |
|------|---------|
| `0` | All tests passed successfully |
| `100` | Tests completed with failures |
| `101` | Tests completed with warnings only |

### Shell Script Handling

```bash
#!/bin/bash

# Run tests and handle exit codes
./addons/gdUnit4/runtest.sh -a res://tests
exit_code=$?

if [ $exit_code -eq 0 ]; then
    echo "✅ All tests passed"
    exit 0
elif [ $exit_code -eq 101 ]; then
    echo "⚠️ Tests completed with warnings"
    # Treat warnings as success or failure based on your needs
    exit 0  # or exit 1
else
    echo "❌ Tests failed with exit code $exit_code"
    exit 1
fi
```

### GitLab CI Example with Exit Code Handling

```yaml
gdunit4:
  stage: tests
  script:
    - export GODOT_BIN=/usr/local/bin/godot
    - ./addons/gdUnit4/runtest.sh -a ./test || if [ $? -eq 101 ]; then echo "warnings"; elif [ $? -eq 0 ]; then echo "success"; else exit 1; fi
  artifacts:
    when: always
    reports:
      junit: ./reports/report_1/results.xml
```

---

## 5. Jenkins Integration

### Pipeline Example (Declarative)

```groovy
pipeline {
    agent any
    
    environment {
        GODOT_BIN = '/usr/local/bin/godot'
        GODOT_VERSION = '4.6'
        DOTNET_VERSION = '8.0'
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Setup') {
            steps {
                sh 'chmod +x ./addons/gdUnit4/runtest.sh'
                sh 'dotnet restore'  // For C# projects
            }
        }
        
        stage('Build') {
            steps {
                sh 'dotnet build --configuration Release'
            }
        }
        
        stage('Test') {
            steps {
                sh '''
                    ./addons/gdUnit4/runtest.sh \
                        -a res://tests \
                        -c \
                        -rd ./test-reports
                '''
            }
            post {
                always {
                    // Archive JUnit XML report
                    junit allowEmptyResults: true, 
                          testResults: 'test-reports/**/results.xml',
                          healthScaleFactor: 1.0
                    
                    // Archive HTML report
                    archiveArtifacts allowEmptyArchive: true, 
                                     artifacts: 'test-reports/**/*',
                                     fingerprint: true
                }
            }
        }
    }
    
    post {
        always {
            // Clean up
            cleanWs()
        }
    }
}
```

### Jenkins JUnit Plugin Configuration

The `junit` step accepts JUnit XML reports:

```groovy
junit(
    testResults: 'reports/**/results.xml',
    allowEmptyResults: true,
    healthScaleFactor: 1.0,
    keepLongStdio: true,
    skipMarkingBuildUnstable: false
)
```

### Jenkins Parameters

| Parameter | Description |
|-----------|-------------|
| `testResults` | Ant glob pattern for XML files |
| `allowEmptyResults` | Don't fail on missing files |
| `healthScaleFactor` | Impact on build health (0.0-10.0) |
| `keepLongStdio` | Keep full test output |
| `skipMarkingBuildUnstable` | Keep build successful on failures |

### Jenkinsfile with Matrix Testing

```groovy
pipeline {
    agent any
    
    matrix {
        axes {
            axis {
                name 'GODOT_VERSION'
                values '4.5', '4.6'
            }
            axis {
                name 'DOTNET_VERSION'
                values 'net7.0', 'net8.0'
            }
        }
        
        stages {
            stage('Test') {
                steps {
                    sh "./addons/gdUnit4/runtest.sh -a res://tests"
                }
                post {
                    always {
                        junit "reports/**/results.xml"
                    }
                }
            }
        }
    }
}
```

---

## 6. GitHub Actions Integration

### Official Action: `godot-gdunit-labs/gdUnit4-action@v1`

**Marketplace:** https://github.com/marketplace/actions/gdunit4-test-runner-action  
**Repository:** https://github.com/godot-gdunit-labs/gdUnit4-action

### Basic GDScript Testing

```yaml
name: GdUnit4 Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      
      - uses: godot-gdunit-labs/gdUnit4-action@v1
        with:
          godot-version: '4.6'
          paths: 'res://tests'
          timeout: 10
```

### C#/.NET Testing with .NET 8.0

```yaml
name: GdUnit4 C# Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      
      - uses: godot-gdunit-labs/gdUnit4-action@v1
        with:
          godot-version: '4.6'
          godot-net: true
          dotnet-version: 'net8.0'
          paths: 'res://tests'
          timeout: 15
          console-verbosity: 'normal'
```

### Full Configuration Reference

```yaml
- uses: godot-gdunit-labs/gdUnit4-action@v1
  with:
    # Required
    paths: 'res://tests'                    # Test directories
    
    # Godot Configuration
    godot-version: '4.6'                    # Required: Godot version
    godot-status: 'stable'                  # stable/rc1/dev1
    godot-net: true                         # Enable for C# tests
    godot-force-mono: false                 # Force mono for GDScript
    
    # .NET Configuration
    dotnet-version: 'net8.0'                # net7.0 or net8.0
    console-verbosity: 'minimal'            # quiet/minimal/normal/detailed/diagnostic
    
    # Test Configuration
    version: 'latest'                       # GDUnit4 plugin version
    timeout: 10                             # Minutes
    retries: 0                              # Retry count for flaky tests
    arguments: '--verbose'                  # Additional arguments
    warnings-as-errors: false               # Treat warnings as failures
    
    # Reporting Configuration
    publish-report: true                    # Enable report publishing
    upload-report: true                     # Upload as artifact
    report-name: 'test-report.xml'          # Report filename
    
    # Project Structure
    project_dir: './'                       # Project directory
```

### Matrix Testing Example

```yaml
name: Matrix Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        godot-version: ['4.5', '4.6']
        dotnet-version: ['net7.0', 'net8.0']
    
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      
      - uses: godot-gdunit-labs/gdUnit4-action@v1
        with:
          godot-version: ${{ matrix.godot-version }}
          godot-net: true
          dotnet-version: ${{ matrix.dotnet-version }}
          paths: 'res://tests'
```

### With Warnings as Errors

```yaml
- uses: godot-gdunit-labs/gdUnit4-action@v1
  with:
    godot-version: '4.6'
    paths: 'res://tests'
    warnings-as-errors: true
```

### Complete Workflow Example

```yaml
name: CI
run-name: ${{ github.head_ref || github.ref_name }}-ci

on:
  pull_request:
    paths-ignore:
      - '**.yml'
      - '**.md'
  workflow_dispatch:

concurrency:
  group: ci-${{ github.event.number }}
  cancel-in-progress: true

jobs:
  unit-test:
    name: "Unit Tests"
    runs-on: ubuntu-22.04
    timeout-minutes: 15
    
    permissions:
      actions: write
      checks: write
      contents: write
      pull-requests: write
      statuses: write

    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      
      - uses: godot-gdunit-labs/gdUnit4-action@v1
        with:
          godot-version: '4.6'
          godot-net: true
          dotnet-version: 'net8.0'
          paths: |
            res://tests/
          timeout: 10
          report-name: test_report.xml
          warnings-as-errors: true
```

---

## 7. Badge Generation

### Using Shields.io

**Website:** https://shields.io/

### Static Badges

```markdown
![Tests](https://img.shields.io/badge/tests-passing-brightgreen)
![Tests](https://img.shields.io/badge/tests-failing-red)
```

### GitHub Actions Status Badge

```markdown
![CI](https://github.com/USERNAME/REPO/actions/workflows/ci.yml/badge.svg)
```

### Custom Badge with Shields.io

```markdown
![Godot](https://img.shields.io/badge/Godot-4.6-%23478cbf?logo=godot-engine&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)
![GDUnit4](https://img.shields.io/badge/GDUnit4-v6.1-blue)
```

### Dynamic Test Coverage Badge

For GitHub Actions, use the workflow status:

```markdown
![Tests](https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/test.yml/badge.svg?branch=main)
```

### Jenkins Badge

Use Jenkins built-in badge or Shields.io endpoint:

```markdown
![Build](https://img.shields.io/jenkins/build?jobUrl=https://your-jenkins.com/job/your-job)
```

### Example README Badges Section

```markdown
# Project Name

![Godot](https://img.shields.io/badge/Godot-4.6-%23478cbf?logo=godot-engine&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)
![Tests](https://github.com/USERNAME/REPO/actions/workflows/test.yml/badge.svg)
![License](https://img.shields.io/badge/License-MIT-blue)
```

---

## 8. C#/.NET Specific Configuration

### Project Requirements

For C# testing with GDUnit4:

1. **Godot .NET Version** - Use the mono/.NET build of Godot
2. **.NET SDK** - Install .NET 7.0 or 8.0 SDK
3. **GDUnit4Net** - C# API for writing tests

### C# Test Example

```csharp
using GdUnit4;
using static GdUnit4.Assertions;

namespace Tests;

[TestSuite]
public class ExampleTest
{
    [TestCase]
    public void TestExample()
    {
        AssertThat("Hello World")
            .HasLength(11)
            .StartsWith("Hello");
    }
    
    [TestCase]
    [RequireGodotRuntime]  // Use when Godot APIs are needed
    public void TestWithGodot()
    {
        var node = new Node();
        AssertThat(node).IsNotNull();
    }
}
```

### VSTest Integration

GDUnit4 supports IDE integration via VSTest adapter:

- **Visual Studio**
- **Visual Studio Code**
- **JetBrains Rider**

See: https://github.com/godot-gdunit-labs/gdUnit4Net

### .csproj Configuration

Ensure your test project references GDUnit4:

```xml
<Project Sdk="Godot.NET.Sdk/4.6.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="gdUnit4.api" Version="6.1.*" />
  </ItemGroup>
</Project>
```

### Console Verbosity Levels

For C# tests, control output verbosity:

| Level | Description |
|-------|-------------|
| `quiet` | Minimal output |
| `minimal` | Default, summary only |
| `normal` | Standard detail |
| `detailed` | More verbose |
| `diagnostic` | Full debug output |

---

## Summary

### Quick Reference

| Task | Command/Configuration |
|------|----------------------|
| Run tests locally | `./addons/gdUnit4/runtest.sh -a res://tests` |
| Exit code success | `0` |
| Exit code failures | `100` |
| Exit code warnings | `101` |
| JUnit report | `reports/**/results.xml` |
| HTML report | `reports/index.html` |
| GitHub Action | `godot-gdunit-labs/gdUnit4-action@v1` |
| Jenkins step | `junit 'reports/**/results.xml'` |

### Compatibility Matrix

| GDUnit4 Version | Godot Version |
|-----------------|---------------|
| v6.1.x | 4.5, 4.5.1, 4.6 |
| v6.0.x | 4.5, 4.5.1 |
| v5.x | 4.3, 4.4, 4.4.1 |

---

## Resources

- **GDUnit4 Documentation:** https://godot-gdunit-labs.github.io/gdUnit4/latest/
- **GDUnit4 GitHub:** https://github.com/godot-gdunit-labs/gdUnit4
- **GitHub Action:** https://github.com/marketplace/actions/gdunit4-test-runner-action
- **Discord:** https://discord.gg/rdq36JwuaJ
- **Shields.io:** https://shields.io/
- **JUnit XML Format:** https://www.ibm.com/docs/en/developer-for-zos/14.1.0?topic=formats-junit-xml-format
