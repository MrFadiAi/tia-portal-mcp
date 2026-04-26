using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Siemens.Engineering;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using WorkerTiaPortalSession = TiaMcpServer.OpennessWorker.Openness.TiaPortalSession;

namespace TiaMcpServer.OpennessWorker;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static Program()
    {
        AssemblyResolver.Register();
    }

    private static void Main()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string? line;
        while ((line = Console.In.ReadLine()) is not null)
        {
            var response = HandleLine(line);
            Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            Console.Out.Flush();
        }
    }

    private static WorkerResponse HandleLine(string line)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WorkerRequest>(line, JsonOptions);
            if (request is null)
            {
                return Failure("Worker request was empty.");
            }

            if (!string.Equals(request.Method, "browse_project_tree", StringComparison.Ordinal))
            {
                return Failure($"Unsupported worker method '{request.Method}'.");
            }

            return BrowseProjectTree(request.ProjectPath);
        }
        catch (JsonException ex)
        {
            return Failure($"Worker request was invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return Failure(ex.Message);
        }
    }

    private static WorkerResponse BrowseProjectTree(string? projectPath)
    {
        try
        {
            using var session = new WorkerTiaPortalSession();
            var walker = new ProjectTreeWalker();

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(projectPath))
            {
                session.OpenProject(projectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var tree = walker.Walk(session.Project);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(tree, JsonOptions)
            };
        }
        catch (EngineeringException ex)
        {
            return Failure($"TIA Portal operation failed: {ex.Message}");
        }
        catch (NonRecoverableException ex)
        {
            return Failure($"TIA Portal was closed unexpectedly: {ex.Message}. Please restart TIA Portal and try again.");
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message);
        }
        catch (System.IO.IOException ex)
        {
            return Failure(ex.Message);
        }
    }

    private static WorkerResponse Failure(string error)
    {
        return new WorkerResponse
        {
            Success = false,
            Error = error
        };
    }
}
