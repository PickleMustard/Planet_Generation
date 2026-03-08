pipeline {
    agent any
    
    environment {
        // Godot Configuration
        GODOT_VERSION = '4.6'
        GODOT_RELEASE = 'stable'
        GODOT_BINARY = "${WORKSPACE}/.godot-ci/godot"
        
        // .NET Configuration
        DOTNET_VERSION = '8.0'
        
        // Test Configuration
        TEST_DIRECTORY = 'res://Tests'
        REPORT_DIRECTORY = 'test-reports'
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    // Check if LFS is being used
                    if (fileExists('.gitattributes')) {
                        sh 'git lfs pull || true'
                    }
                }
            }
        }
        
        stage('Setup Environment') {
            steps {
                sh '''
                    # Make test runner executable
                    chmod +x ./addons/gdUnit4/runtest.sh
                    
                    # Create CI tools directory
                    mkdir -p .godot-ci
                '''
            }
        }
        
        stage('Download Godot') {
            steps {
                sh '''
                    # Download Godot .NET edition (required for C# projects)
                    GODOT_URL="https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-${GODOT_RELEASE}/Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_x86_64.zip"
                    
                    echo "Downloading Godot ${GODOT_VERSION} .NET edition..."
                    curl -L "$GODOT_URL" -o .godot-ci/godot.zip
                    
                    # Extract Godot
                    unzip -o .godot-ci/godot.zip -d .godot-ci
                    
                    # Find and link the Godot binary (handle versioned filename)
                    GODOT_EXEC=$(find .godot-ci -name "Godot_v*" -type f -executable | head -n1)
                    if [ -n "$GODOT_EXEC" ]; then
                        ln -sf "$GODOT_EXEC" .godot-ci/godot
                        chmod +x "$GODOT_EXEC"
                        echo "Godot binary: $GODOT_EXEC"
                    else
                        echo "ERROR: Could not find Godot executable"
                        exit 1
                    fi
                '''
            }
        }
        
        stage('Restore Dependencies') {
            steps {
                sh 'dotnet restore'
            }
        }
        
        stage('Build') {
            steps {
                sh 'dotnet build --configuration Release --no-restore'
            }
        }
        
        stage('Run Tests') {
            environment {
                GODOT_BIN = "${WORKSPACE}/.godot-ci/godot"
            }
            steps {
                sh '''
                    # Verify Godot is available
                    if [ ! -x "$GODOT_BIN" ]; then
                        echo "ERROR: Godot binary not found or not executable"
                        exit 1
                    fi
                    
                    echo "Using Godot: $GODOT_BIN"
                    $GODOT_BIN --version
                    
                    # Run GDUnit4 tests
                    # -a: Add test directory
                    # -c: Continue on failure (run all tests for complete report)
                    ./addons/gdUnit4/runtest.sh \
                        --godot_binary "$GODOT_BIN" \
                        -a "${TEST_DIRECTORY}" \
                        -c \
                        -rd "./${REPORT_DIRECTORY}"
                '''
            }
            post {
                always {
                    // Publish JUnit test results for Jenkins test trending
                    junit allowEmptyResults: true,
                          testResults: "${REPORT_DIRECTORY}/**/results.xml",
                          healthScaleFactor: 1.0,
                          skipMarkingBuildUnstable: false
                    
                    // Archive HTML reports for detailed failure analysis
                    archiveArtifacts allowEmptyArchive: true,
                                     artifacts: "${REPORT_DIRECTORY}/**/*",
                                     fingerprint: true
                }
            }
        }
    }
    
    post {
        always {
            echo "Test reports available at: ${REPORT_DIRECTORY}/index.html"
        }
        
        failure {
            echo '❌ Pipeline failed! Check the test reports for details.'
        }
        
        success {
            echo '✅ All tests passed successfully!'
        }
        
        cleanup {
            // Archive the full report directory as a zip for easy download
            zip glob: "${REPORT_DIRECTORY}/**/*", zipFile: 'test-reports.zip'
            archiveArtifacts artifacts: 'test-reports.zip', allowEmptyArchive: true
        }
    }
}
