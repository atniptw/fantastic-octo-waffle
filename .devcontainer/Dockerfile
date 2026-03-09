FROM mcr.microsoft.com/devcontainers/python:1-3.11-bookworm

RUN find /etc/apt/sources.list.d -maxdepth 1 -type f -name '*yarn*' -delete \
    && rm -f /etc/apt/keyrings/yarn.gpg \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        libgl1 \
        libglib2.0-0 \
        libsm6 \
        libxext6 \
        libxrender1 \
    && rm -rf /var/lib/apt/lists/*
