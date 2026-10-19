Run `prepare_smoke.ps1` from PowerShell to stage an isolated Unity project under
`Temp/IntroSmokeProject`. The user's open scene is not used by the checks.

Launch Unity 6000.3 with `-batchmode -projectPath <staged-project>` and
`-executeMethod IntroSmokeEditor.Run -logFile <log-path>` (without `-quit`).
The runner exits Unity and writes `smoke-result.txt` inside the staged project.

Checks use the actual intro video and production transition/pause scripts with
small test scenes. UIManager and event declarations are stubs; run the existing
`Tools/CompileCheck/CompileCheck.csproj` as well to compile all gameplay scripts.
The runner seeks across the 27-second boundary and near the end to avoid waiting
through the entire intro. It also checks duplicate starts, missing destinations,
missing video, decoder errors, cross-scene survival, and pause/input restoration.
