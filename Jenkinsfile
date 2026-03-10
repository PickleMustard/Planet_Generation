// Define a unique label for the pod so Jenkins can schedule it
def podLabel = "godot-build-pod-${UUID.randomUUID().toString()}"

// Configuration
def GODOT_VERSION = '4.6'
def GODOT_RELEASE = 'stable'
def TEST_DIRECTORY = 'res://Tests'
def REPORT_DIRECTORY = 'test-reports'

podTemplate(label: podLabel, yaml: """
apiVersion: v1
kind: Pod
spec:
  containers:
  # 1. Default Jenkins Agent
  - name: jnlp
    image: jenkins/inbound-agent:latest

  # 2. .NET SDK Container for building and testing
  - name: dotnet
    image: mcr.microsoft.com/dotnet/sdk:8.0
    command:
    - cat
    tty: true
    resources:
      requests:
        memory: "2Gi"
        cpu: "1"
      limits:
        memory: "4Gi"
        cpu: "2"

""") {
    node(podLabel) {
        try {
            stage('Checkout') {
                checkout scm
                // Pull LFS assets if configured
                sh 'git lfs pull || true'
            }

            stage('Setup Environment') {
                container('dotnet') {
                    sh '''
                        # Install required tools for downloading Godot
                        apt-get update && apt-get install -y curl unzip libxi6 libxrender1 libgl1-mesa-glx > /dev/null 2>&1
                        
                        # Make test runner executable
                        chmod +x ./addons/gdUnit4/runtest.sh
                        
                        # Create CI tools directory
                        mkdir -p .godot-ci
                    '''
                }
            }

            stage('Download Godot') {
                container('dotnet') {
                    sh """
                        # Download Godot .NET edition (required for C# projects)
                        GODOT_URL="https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-${GODOT_RELEASE}/Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_x86_64.zip"
                        
                        echo "Downloading Godot ${GODOT_VERSION} .NET edition..."
                        curl -L "\$GODOT_URL" -o .godot-ci/godot.zip
                        
                        # Extract Godot
                        unzip -o .godot-ci/godot.zip -d .godot-ci
                        
                        # Find and link the Godot binary (handle versioned filename)
                        GODOT_EXEC=\$(find .godot-ci -name "Godot_v*" -type f -executable | head -n1)
                        if [ -n "\$GODOT_EXEC" ]; then
                            ln -sf "\$GODOT_EXEC" .godot-ci/godot
                            chmod +x "\$GODOT_EXEC"
                            echo "Godot binary: \$GODOT_EXEC"
                        else
                            echo "ERROR: Could not find Godot executable"
                            exit 1
                        fi
                    """
                }
            }

            stage('Restore Dependencies') {
                container('dotnet') {
                    sh 'dotnet restore'
                }
            }

            stage('Build') {
                container('dotnet') {
                    sh 'dotnet build --configuration Release --no-restore'
                }
            }

            stage('Test') {
                parallel(
                    "GDUnit4 Tests": {
                        container('dotnet') {
                            sh """
                                # Set Godot binary path
                                export GODOT_BIN="\${WORKSPACE}/.godot-ci/godot"
                                
                                # Verify Godot is available
                                if [ ! -x "\$GODOT_BIN" ]; then
                                    echo "ERROR: Godot binary not found or not executable"
                                    exit 1
                                fi
                                
                                echo "Using Godot: \$GODOT_BIN"
                                \$GODOT_BIN --version
                                
                                # Run GDUnit4 tests
                                # -a: Add test directory
                                # -c: Continue on failure (run all tests for complete report)
                                ./addons/gdUnit4/runtest.sh \\
                                    --godot_binary "\$GODOT_BIN" \\
                                    -a "${TEST_DIRECTORY}" \\
                                    -c \\
                                    -rd "./${REPORT_DIRECTORY}"
                            """
                        }
                    }
                )
            }

            echo '✅ All tests passed successfully!'

        } catch (Exception e) {
            echo '❌ Pipeline failed! Check the test reports for details.'
            throw e
        } finally {
            // Publish JUnit test results for Jenkins test trending
            junit allowEmptyResults: true,
                  testResults: "${REPORT_DIRECTORY}/**/results.xml",
                  healthScaleFactor: 1.0,
                  skipMarkingBuildUnstable: false
            
            // Archive HTML reports for detailed failure analysis
            archiveArtifacts allowEmptyArchive: true,
                             artifacts: "${REPORT_DIRECTORY}/**/*",
                             fingerprint: true
            
            // Archive as zip for easy download
            zip glob: "${REPORT_DIRECTORY}/**/*", zipFile: 'test-reports.zip'
            archiveArtifacts artifacts: 'test-reports.zip', allowEmptyArchive: true
            
            echo "Test reports available at: ${REPORT_DIRECTORY}/index.html"
        }
    }
}
