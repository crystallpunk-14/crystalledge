#!/bin/bash
cd "$(dirname "$0")/.."

echo "========================================"
echo "  SS14 Prototype Redactor"
echo "========================================"
echo ""
echo "Starting Redactor editor at http://localhost:5555/"
echo "Press Ctrl+C to stop the server."
echo ""

dotnet "bin/Content.Redactor/Content.Redactor.dll" serve "$(pwd)"
