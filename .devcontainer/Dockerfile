FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

# Remove problematic Yarn repository that has expired GPG key
RUN rm -f /etc/apt/sources.list.d/yarn.list && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Ensure vscode user exists and has proper permissions
USER root
RUN usermod -s /bin/bash vscode 2>/dev/null || true

# Switch to vscode user
USER vscode
