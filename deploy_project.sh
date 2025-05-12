# Function to display error message and exit
error_exit() {
    echo "$1" >&2
    exit 1
}

# Check if gh is installed
if ! command -v gh &> /dev/null; then
  error_exit "gh is not installed, make sure you run 'make init' (see README.md)."
fi

# When this script is called from release workflow, the version is passed as an argument, otherwise it will be read from the app/build.gradle file
PACKAGE_VERSION=$1
PACKAGE_NAME="sentry-defenses"
REPO=sentry-demos/unity

# Check if current version was already released
currentVersion=$(gh release list | awk '{print $1}' | grep -x "$PACKAGE_VERSION")
if [ "$currentVersion" != "" ]; then
  error_exit "Version $PACKAGE_VERSION was already released."
fi

echo "Releasing to Github..."
gh release create $PACKAGE_VERSION sentry-defenses/Builds/Sentry-Defenses.apk -R $REPO -t "$PACKAGE_VERSION" --generate-notes || error_exit "Failed to create GitHub release."

echo "Release created successfully with version $PACKAGE_VERSION!"