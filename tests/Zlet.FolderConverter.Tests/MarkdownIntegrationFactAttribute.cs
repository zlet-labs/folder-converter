using System;
using Xunit;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MarkdownIntegrationFactAttribute : FactAttribute
{
    public MarkdownIntegrationFactAttribute()
    {
        var runner = new DoclingWorkerProcessRunner();
        if (!runner.IsAvailable)
        {
            Skip = "Markdown runtime is not available (install optional Markdown component to run integration tests).";
        }
    }
}
