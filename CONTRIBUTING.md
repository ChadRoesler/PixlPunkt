# Contributing to PixlPunkt

First off, thank you for considering contributing to PixlPunkt! ??

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Guidelines](#coding-guidelines)
- [Commit Messages](#commit-messages)
- [Pull Request Process](#pull-request-process)

---

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code:

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on constructive feedback
- Accept responsibility for mistakes and learn from them

---

## How Can I Contribute?

### Reporting Bugs

Before creating a bug report:
1. Check the [existing issues](https://github.com/ChadRoesler/PixlPunkt/issues) to avoid duplicates
2. Collect information about the bug:
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots if applicable
   - Your OS version and PixlPunkt version

Then [create a new issue](https://github.com/ChadRoesler/PixlPunkt/issues/new?template=bug_report.md) using the bug report template.

### Suggesting Features

Feature requests are welcome! Please:
1. Check if the feature is already requested or planned
2. Describe the use case and why it would be valuable
3. [Create a feature request](https://github.com/ChadRoesler/PixlPunkt/issues/new?template=feature_request.md)

### Code Contributions

Areas where we'd love help:
- Bug fixes
- New effects or tools
- Performance improvements
- Documentation
- Plugin examples
- UI/UX improvements

---

## Development Setup

### Prerequisites
- Windows 10/11
- Visual Studio 2022 (17.0+) with:
  - .NET Desktop Development workload
  - Windows App SDK
- .NET 10 SDK

### Getting Started

1. **Fork and clone**
   ```bash
   git clone https://github.com/YOUR-USERNAME/PixlPunkt.git
   cd PixlPunkt
   ```

2. **Open in Visual Studio**
   - Open `PixlPunkt.sln`
   - Restore NuGet packages (automatic)

3. **Build and run**
   - Set `PixlPunkt` as the startup project
   - Press F5 to build and debug

### Project Structure

| Project | Description |
|---------|-------------|
| `PixlPunkt` | Main WinUI 3 application |
| `PixlPunkt.PluginSdk` | SDK for plugin development |
| `PixlPunkt.ExamplePlugin` | Reference plugin implementation |

---

## Coding Guidelines

### General Principles
- Write clean, readable code
- Follow existing patterns in the codebase
- Keep methods focused and small
- Prefer composition over inheritance

### C# Style
- Use C# 14 features where appropriate
- Enable nullable reference types
- Use `var` when the type is obvious
- Prefer expression-bodied members for simple properties/methods

### Naming Conventions
```csharp
// Classes and interfaces
public class PixelSurface { }
public interface IStrokePainter { }

// Methods and properties
public void UpdatePreview() { }
public int Width { get; }

// Private fields
private readonly int _brushSize;
private bool _isPainting;

// Constants
private const int MaxBrushSize = 64;
```

### Documentation
- Add XML docs for all public APIs
- Include `<summary>`, `<param>`, and `<returns>` where applicable
- Use `<remarks>` for additional context

```csharp
/// <summary>
/// Writes a BGRA color to the specified coordinates.
/// </summary>
/// <param name="x">The X coordinate (0-based).</param>
/// <param name="y">The Y coordinate (0-based).</param>
/// <param name="bgra">The color in BGRA format.</param>
public void WriteBGRA(int x, int y, uint bgra) { }
```

### Error Handling
- Use specific exception types
- Log errors with context
- Never use empty catch blocks

```csharp
// Good
catch (IOException ex)
{
    LoggingService.Error("Failed to save document", ex);
    throw;
}

// Avoid
catch { }
```

---

## Commit Messages

Follow the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style (formatting, no logic change)
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `test`: Adding tests
- `chore`: Build, CI, or tooling changes

### Examples
```
feat(tools): add gradient fill tool

fix(layers): prevent crash when deleting last layer

docs(sdk): update plugin development guide

refactor(core): simplify DocumentIO by removing legacy loaders
```

---

## Pull Request Process

1. **Create a branch**
   ```bash
   git checkout -b feature/my-feature
   # or
   git checkout -b fix/bug-description
   ```

2. **Make your changes**
   - Follow coding guidelines
   - Add/update tests if applicable
   - Update documentation if needed

3. **Test locally**
   - Build the solution
   - Run the application
   - Test your changes thoroughly

4. **Commit and push**
   ```bash
   git add .
   git commit -m "feat(scope): description"
   git push origin feature/my-feature
   ```

5. **Create a Pull Request**
   - Use the PR template
   - Link related issues
   - Provide clear description of changes

6. **Code Review**
   - Address reviewer feedback
   - Keep commits clean (squash if needed)
   - Be patient and responsive

### PR Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Documentation updated (if applicable)
- [ ] No new warnings introduced
- [ ] Changes tested locally

---

## Questions?

Feel free to:
- Open a [Discussion](https://github.com/ChadRoesler/PixlPunkt/discussions)
- Ask in an issue comment
- Reach out to maintainers

Thank you for contributing! ??
