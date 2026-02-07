#!/bin/bash
set -e

echo "🚀 Setting up R.E.P.O. Mod Browser dev environment..."

# Ensure Git LFS is installed and initialized
if command -v git-lfs >/dev/null 2>&1; then
	echo "📦 Initializing Git LFS..."
	git lfs install 2>&1 || echo "⚠️  Git LFS initialization warning"
else
	echo "⚠️  Git LFS not found. Ensure devcontainer rebuild completes."
fi

# Install .NET Blazor WebAssembly workload
echo "📦 Installing Blazor WebAssembly workload..."
dotnet workload install wasm-tools 2>&1 || echo "⚠️  Blazor workload installation warning (may already be installed)"

# Restore .NET dependencies
echo "📦 Restoring .NET dependencies..."
dotnet restore 2>&1 || { echo "❌ dotnet restore failed"; exit 1; }

# Install Node.js dependencies for Cloudflare Worker
echo "📦 Installing Cloudflare Worker dependencies..."
cd cloudflare-worker
npm install 2>&1 || { echo "❌ npm install failed for worker"; exit 1; }
cd ..

# Install Cloudflare Wrangler CLI globally
echo "📦 Installing Cloudflare Wrangler CLI..."
npm install -g wrangler 2>&1 || { echo "❌ Wrangler CLI installation failed"; exit 1; }

# Install Python dependencies for validation scripts
echo "📦 Installing Python dependencies..."
pip3 install UnityPy pyyaml jsonschema 2>&1 || echo "⚠️  Python dependencies installation warning"

# Build the solution to verify setup (allow failure for now)
echo "🔨 Building solution..."
dotnet build 2>&1 || echo "⚠️  Build had warnings/errors - you may need to fix them manually"

echo "✅ Dev environment setup complete!"
echo ""
echo "📋 Quick Start:"
echo "  - Run Blazor app:    dotnet watch run (from src/BlazorApp)"
echo "  - Run tests:         dotnet test"
echo "  - Start worker:      wrangler dev (from cloudflare-worker)"
echo ""
echo "🌐 Ports:"
echo "  - Blazor (HTTP):  http://localhost:5000"
echo "  - Blazor (HTTPS): https://localhost:5001"
echo "  - Worker (Dev):   http://localhost:8787"
