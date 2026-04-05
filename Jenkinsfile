pipeline {
    agent none

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        NUGET_XMLDOC_MODE = 'skip'
    }

    stages {
        stage('Build & Unit Tests') {
            agent { label 'docker' }
            steps {
                sh 'dotnet restore "Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln"'
                sh 'dotnet build "Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.sln" -c Debug --no-restore'

                sh '''
                    dotnet test "Source/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests/DotNetWorkQueue.TaskScheduling.Distributed.TaskScheduler.Tests.csproj" \
                        -f net8.0 --no-build -c Debug \
                        --collect:"XPlat Code Coverage" --results-directory coverage/unit
                '''

                sh '''
                    dotnet tool install -g dotnet-reportgenerator-globaltool --version 5.* || true
                    export PATH="$PATH:$HOME/.dotnet/tools"

                    reportgenerator \
                        -reports:"coverage/**/coverage.cobertura.xml" \
                        -targetdir:"coverage/report" \
                        -reporttypes:"HtmlInline;Cobertura"

                    echo "Coverage report generated at coverage/report/"
                '''

                withCredentials([string(credentialsId: 'codecov-token', variable: 'CODECOV_TOKEN')]) {
                    sh '''
                        curl -Os https://cli.codecov.io/latest/linux/codecov
                        chmod +x codecov
                        ./codecov upload-process --file coverage/report/Cobertura.xml --token "$CODECOV_TOKEN" || echo "Codecov upload failed (non-fatal)"
                    '''
                }

                publishHTML(target: [
                    allowMissing: true,
                    alwaysLinkToLastBuild: true,
                    keepAll: true,
                    reportDir: 'coverage/report',
                    reportFiles: 'index.html',
                    reportName: 'Code Coverage'
                ])
            }
        }
    }

    post {
        failure {
            echo 'Pipeline failed. Check stage logs for details.'
        }
        success {
            echo 'Pipeline completed successfully.'
        }
    }
}
