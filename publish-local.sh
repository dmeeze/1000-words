#!/bin/zsh

# Local publish script for testing
# This script publishes the app with a configurable base href for local testing

BASE_HREF="${1:-/}"

echo "Publishing Blazor WASM app with BASE_HREF=${BASE_HREF}..."

# Clean previous publish
rm -rf publish

# Publish the app
dotnet publish Llm64.Wasm/Llm64.Wasm.csproj -c Release -o publish -p:BaseHref="${BASE_HREF}"

# Replace __BASE_HREF__ placeholder in all HTML files
echo "Replacing __BASE_HREF__ with ${BASE_HREF} in HTML files..."
find publish/wwwroot -name "*.html" -type f -exec sed -i '' "s|__BASE_HREF__|${BASE_HREF}|g" {} \;

echo ""
echo "✅ Published successfully to: publish/wwwroot"
echo ""

# Verify static assets were copied
if [ -d "publish/wwwroot/img" ]; then
    IMG_COUNT=$(ls publish/wwwroot/img/*.png 2>/dev/null | wc -l | tr -d ' ')
    echo "📁 Static assets: ${IMG_COUNT} images in /img/ directory"
else
    echo "⚠️  Warning: /img/ directory not found in publish output"
fi

echo ""
echo "To test locally, run:"
echo "  cd publish/wwwroot && python3 -m http.server 8080"
echo ""
echo "Then visit:"
if [ "${BASE_HREF}" = "/" ]; then
    echo "  http://localhost:8080/"
    echo "  http://localhost:8080/about.html"
else
    # Remove trailing slash for display
    CLEAN_PATH=$(echo "${BASE_HREF}" | sed 's|/$||')
    echo "  http://localhost:8080${CLEAN_PATH}/"
    echo "  http://localhost:8080${CLEAN_PATH}/about.html"
fi
echo ""
