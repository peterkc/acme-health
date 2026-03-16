# AcmeHealth quality targets
# Usage: just <target>
# Run `just --list` to see all available targets.

# Default: run full quality check
default: check

# --- Build ---

# Build all C# projects
build:
    dotnet build AcmeHealth.slnx

# --- Test ---

# Run all tests (C# + Python) — depends on build for --no-build to work
test: build test-dotnet test-py

# Run C# tests (skip integration tests that need a live DB)
test-dotnet:
    dotnet test AcmeHealth.slnx --no-build --filter "Category!=Integration"

# Run Python tests (wearable-normalizer only; clinical-extractor has no tests yet)
test-py:
    cd src/services/wearable-normalizer && uv run pytest tests/ -v -m "not integration"

# --- Lint ---

# Run all linters (C# format check + ruff)
lint: lint-dotnet lint-py

# Check C# formatting (no changes, just verify)
lint-dotnet:
    dotnet format AcmeHealth.slnx --verify-no-changes

# Check Python code with ruff
lint-py:
    ruff check src/services/

# --- Format ---

# Format all code (C# + Python)
fmt: fmt-dotnet fmt-py

# Format C# code
fmt-dotnet:
    dotnet format AcmeHealth.slnx

# Format Python code
fmt-py:
    ruff format src/services/
    ruff check --fix --select I src/services/

# Check Python formatting without making changes (C# checked via lint-dotnet)
fmt-check: fmt-check-py

# Check Python formatting without changes
fmt-check-py:
    ruff format --check src/services/
    ruff check --select I src/services/

# --- Full Quality Gate ---

# Run all quality gates: build, test, lint, format-check
check: build test lint fmt-check
