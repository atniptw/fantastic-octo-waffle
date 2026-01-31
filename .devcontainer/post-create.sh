#!/bin/bash
set -e

echo "🚀 Setting up R.E.P.O. Mod Browser dev environment..."

# Install .NET Blazor WebAssembly workload
echo "📦 Installing Blazor WebAssembly workload..."
dotnet workload install wasm-tools

# Restore .NET dependencies
echo "📦 Restoring .NET dependencies..."
dotnet restore

# Install Cloudflare Wrangler CLI globally
echo "📦 Installing Cloudflare Wrangler CLI..."
npm install -g wrangler

# Install Node.js dependencies for Cloudflare Worker
echo "📦 Installing Cloudflare Worker dependencies..."
cd cloudflare-worker
npm install
cd ..

# Install Python dependencies for validation scripts
echo "📦 Installing Python dependencies..."
pip install --user UnityPy pyyaml jsonschema

# Build the solution to verify setup (allow failure for now)
echo "🔨 Building solution..."
dotnet build || echo "⚠️  Build had errors - you may need to fix them manually"

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
