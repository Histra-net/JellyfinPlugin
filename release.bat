@echo off
REM ============================================================================
REM  release.bat — cut a new histra.net Jellyfin plugin release
REM
REM  Usage:   release.bat 1.0.0.1
REM           release.bat            (prompts for the version)
REM
REM  What it does:
REM    1. verifies you are on master with a clean tree, up to date with origin
REM    2. creates tag v<version> and pushes it
REM    3. GitHub Actions (release.yaml) then builds the DLL, packages the ZIP,
REM       creates the GitHub Release, and updates manifest.json automatically
REM ============================================================================
setlocal

set "VERSION=%~1"
if "%VERSION%"=="" set /p "VERSION=Enter version (e.g. 1.0.0.1): "
if "%VERSION%"=="" (
  echo [x] No version given. Aborting.
  exit /b 1
)

REM strip a leading "v" if the user typed v1.0.0.1
if /I "%VERSION:~0,1%"=="v" set "VERSION=%VERSION:~1%"
set "TAG=v%VERSION%"

echo.
echo === Releasing %TAG% ===
echo.

REM --- must be on master ---
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set "BRANCH=%%b"
if /I not "%BRANCH%"=="master" (
  echo [x] You are on "%BRANCH%", not master. Switch to master first.
  exit /b 1
)

REM --- working tree must be clean ---
git diff --quiet && git diff --cached --quiet
if errorlevel 1 (
  echo [x] Working tree has uncommitted changes. Commit or stash first.
  exit /b 1
)

REM --- sync with origin ---
echo Fetching origin...
git fetch origin master
for /f "delims=" %%l in ('git rev-parse HEAD') do set "LOCAL=%%l"
for /f "delims=" %%r in ('git rev-parse origin/master') do set "REMOTE=%%r"
if not "%LOCAL%"=="%REMOTE%" (
  echo [x] Local master is not in sync with origin/master. Pull/push first.
  exit /b 1
)

REM --- tag must not already exist ---
git rev-parse -q --verify "refs/tags/%TAG%" >nul
if not errorlevel 1 (
  echo [x] Tag %TAG% already exists. Bump the version.
  exit /b 1
)

echo Creating and pushing tag %TAG%...
git tag "%TAG%"
git push origin "%TAG%"
if errorlevel 1 (
  echo [x] Failed to push tag.
  exit /b 1
)

echo.
echo === Done. Release pipeline is now running. ===
echo   Watch:   https://github.com/Histra-net/JellyfinPlugin/actions
echo   Release: https://github.com/Histra-net/JellyfinPlugin/releases/tag/%TAG%
echo.
echo After it finishes, manifest.json is updated automatically and Jellyfin
echo clients will see the new version.
endlocal
