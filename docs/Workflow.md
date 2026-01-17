# Workflows (CI/CD)

 

## Build & Test (build.yml)
- Trigger on PR and main
- Build Blazor, run tests
- Validate parsing vs reference

## Deploy (deploy.yml)
- Publish to GitHub Pages
- Update Cloudflare Worker

## Agent Tasks (agent-task.yml)
- Label-based triggers
- Implement porting tasks with Python→C# references
- Create PR, run validations

## Agent Task Template
Include source Python file, lines, target C# file, validation steps.