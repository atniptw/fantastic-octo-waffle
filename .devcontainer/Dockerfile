FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

# Switch to root for system setup
USER root

# Remove problematic Yarn repository before features run (prevents GPG key errors in Python feature)
RUN rm -f /etc/apt/sources.list.d/yarn.list /etc/apt/sources.list.d/*.save && \
    apt-get update && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    usermod -s /bin/bash vscode 2>/dev/null || true

# Switch back to vscode user
USER vscode
