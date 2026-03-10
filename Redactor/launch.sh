#!/bin/bash
cd "$(dirname "$0")/.."

echo "========================================"
echo "  SS14 Prototype Redactor"
echo "========================================"
echo ""
echo "Building project and extracting metadata..."
dotnet build Content.Redactor/Content.Redactor.csproj -c Debug

if [ $? -ne 0 ]; then
    echo ""
    echo "Build failed. Please fix errors and try again."
    exit 1
fi

echo ""
echo "Starting Redactor editor at http://localhost:5555/"
echo "Press Ctrl+C to stop the server."
echo ""

dotnet "bin/Content.Redactor/Content.Redactor.dll" serve "$(pwd)"
