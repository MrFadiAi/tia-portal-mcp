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

            return request.Method switch
            {
                "browse_project_tree" => BrowseProjectTree(request.ProjectPath),
                "read_hardware_config" => ReadHardwareConfig(request.ProjectPath),
                "get_block_content"   => GetBlockContent(request),
                "update_block_logic"  => UpdateBlockLogic(request),
                "list_tag_tables"     => ListTagTables(request),
                _                     => Failure($"Unsupported worker method '{request.Method}'.")
            };
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

    private static WorkerResponse ReadHardwareConfig(string? projectPath)
    {
        try
        {
            using var session = new WorkerTiaPortalSession();

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(projectPath))
            {
                session.OpenProject(projectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var config = HardwareConfigReader.Read(session.Project);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(config, JsonOptions)
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

    private static WorkerResponse GetBlockContent(WorkerRequest request)
    {
        if (string.IsNullOrEmpty(request.BlockPath))
            return Failure("BlockPath is required.");

        try
        {
            using var session = new WorkerTiaPortalSession();

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            string yaml = BlockExporter.Export(session.Project, request.BlockPath!);
            return new WorkerResponse { Success = true, Payload = yaml };
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

    private static WorkerResponse UpdateBlockLogic(WorkerRequest request)
    {
        if (string.IsNullOrEmpty(request.BlockPath))
        {
            return Failure("BlockPath is required.");
        }

        if (string.IsNullOrEmpty(request.YamlContent))
        {
            return Failure("YamlContent is required.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(request.AllowTiaConfirmations);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            string result = BlockImporter.Import(session.Project, request.BlockPath!, request.YamlContent!);
            return new WorkerResponse { Success = true, Payload = result };
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

    private static WorkerResponse ListTagTables(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession();

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var tables = TagTableReader.ReadAll(session.Project, request.PlcName);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(tables, JsonOptions)
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
