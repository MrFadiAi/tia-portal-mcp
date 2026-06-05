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
                "browse_project_tree" => BrowseProjectTree(request),
                "read_hardware_config" => ReadHardwareConfig(request),
#if !LEGACY_TIA_V16
                "search_equipment_catalog" => SearchEquipmentCatalog(request),
                "add_network_device" => AddNetworkDevice(request),
                "configure_network_device" => ConfigureNetworkDevice(request),
                "read_cross_references" => ReadCrossReferences(request),
#endif
                "get_block_content"   => GetBlockContent(request),
                "update_block_logic"  => UpdateBlockLogic(request),
                "list_tag_tables"     => ListTagTables(request),
                "compile_check"       => CompileCheck(request),
                "create_tag_table"    => CreateTagTable(request),
                "delete_tag_table"    => DeleteTagTable(request),
                "create_tag"          => CreateTag(request),
                "update_tag"          => UpdateTag(request),
                "delete_tag"          => DeleteTag(request),
                "create_user_constant" => CreateUserConstant(request),
                "update_user_constant" => UpdateUserConstant(request),
                "delete_user_constant" => DeleteUserConstant(request),
                "get_project_status"  => GetProjectStatus(request),
                "open_project"        => OpenProject(request),
                "create_project"      => CreateProject(request),
                "save_project"        => SaveProject(request),
                "save_project_as"     => SaveProjectAs(request),
                "archive_project"     => ArchiveProject(request),
                "close_project"       => CloseProject(request),
                "read_block_interface" => ReadBlockInterface(request),
                "export_plc_type"     => ExportPlcType(request),
                "export_tag_table_xml" => ExportTagTableXml(request),
                "list_connections"    => ListConnections(request),
                "browse_hmi_screens" => BrowseHmiScreens(request),
                "get_tia_version"     => GetTiaVersion(),
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

    private static WorkerResponse BrowseProjectTree(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);
            var walker = new ProjectTreeWalker();

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
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

    private static WorkerResponse ReadHardwareConfig(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
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

#if !LEGACY_TIA_V16
    private static WorkerResponse SearchEquipmentCatalog(WorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Failure("Query is required.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.TiaPortal is null)
            {
                return Failure("No TIA Portal session is connected. Please start TIA Portal and try again.");
            }

            var entries = EquipmentCatalogSearcher.Search(session.TiaPortal, request.Query!);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(entries, JsonOptions)
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

    private static WorkerResponse AddNetworkDevice(WorkerRequest request)
    {
        if (!CatalogTypeIdentifier.IsCreatable(request.TypeIdentifier))
        {
            return Failure(CatalogTypeIdentifier.BuildValidationMessage(request.TypeIdentifier));
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return Failure("DeviceName is required.");
        }

        if (!request.Confirm)
        {
            return Failure("Operation not confirmed. Set confirm=true to proceed with adding a network device.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(allowTiaConfirmations: true, tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var result = NetworkDeviceCreator.Create(
                session.Project,
                request.TypeIdentifier!,
                request.DeviceName!,
                string.IsNullOrWhiteSpace(request.DeviceItemName) ? request.DeviceName! : request.DeviceItemName!);

            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(result, JsonOptions)
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

    private static WorkerResponse ConfigureNetworkDevice(WorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return Failure("DeviceName is required.");
        }

        if (!request.Confirm)
        {
            return Failure("Operation not confirmed. Set confirm=true to proceed with configuring a network device.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(allowTiaConfirmations: true, tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var result = NetworkDeviceConfigurator.Configure(
                session.Project,
                request.DeviceName!,
                request.IpAddress,
                request.SubnetMask,
                request.PnDeviceName,
                request.SubnetName,
                request.IoSystemName);

            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(result, JsonOptions)
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

    private static WorkerResponse ReadCrossReferences(WorkerRequest request)
    {
        if (!CrossReferenceFilterNames.TryNormalize(
                request.CrossReferenceFilter,
                out var filter,
                out var filterError))
        {
            return Failure(filterError ?? "Invalid cross-reference filter.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var report = CrossReferenceReader.Read(session.Project, request.PlcName, filter);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(report, JsonOptions)
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
#endif

    private static WorkerResponse GetBlockContent(WorkerRequest request)
    {
        if (string.IsNullOrEmpty(request.BlockPath))
            return Failure("BlockPath is required.");

        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

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
            using var session = new WorkerTiaPortalSession(request.AllowTiaConfirmations, request.TiaVersion);

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
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

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

    private static WorkerResponse CompileCheck(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var report = CompileChecker.Compile(session.Project, request.PlcName, request.BlockPath);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(report, JsonOptions)
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

    private static WorkerResponse CreateTagTable(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.CreateTagTable(project, request.PlcName, request.TableName!, request.FolderPath));
    }

    private static WorkerResponse DeleteTagTable(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.DeleteTagTable(project, request.PlcName, request.TableName!, request.FolderPath));
    }

    private static WorkerResponse CreateTag(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.CreateTag(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!,
                request.DataType!,
                request.LogicalAddress));
    }

    private static WorkerResponse UpdateTag(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.UpdateTag(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!,
                request.NewName,
                request.DataType,
                request.LogicalAddress,
                request.ExternalAccessible,
                request.ExternalVisible,
                request.ExternalWritable,
                request.IsSafety));
    }

    private static WorkerResponse DeleteTag(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.DeleteTag(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!));
    }

    private static WorkerResponse CreateUserConstant(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.CreateUserConstant(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!,
                request.DataType!,
                request.Value!));
    }

    private static WorkerResponse UpdateUserConstant(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.UpdateUserConstant(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!,
                request.DataType,
                request.Value));
    }

    private static WorkerResponse DeleteUserConstant(WorkerRequest request)
    {
        return TagMutation(request, project =>
            TagMutationService.DeleteUserConstant(
                project,
                request.PlcName,
                request.TableName!,
                request.FolderPath,
                request.Name!));
    }

    private static WorkerResponse TagMutation(WorkerRequest request, Func<Project, TagMutationResultInfo> mutate)
    {
        if (!request.Confirm)
        {
            return Failure("Operation not confirmed. Set confirm=true to proceed with the tag operation.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(request.AllowTiaConfirmations, request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var result = mutate(session.Project);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(result, JsonOptions)
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

    private static WorkerResponse GetProjectStatus(WorkerRequest request)
    {
        return ProjectLifecycle(request, session =>
        {
            var status = ProjectLifecycleService.GetStatus(session, request.ProjectPath);
            return new ProjectLifecycleResultInfo
            {
                Operation = "get_project_status",
                ProjectPath = status.Path,
                Project = status
            };
        }, requiresConfirm: false);
    }

    private static WorkerResponse OpenProject(WorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return Failure("ProjectPath is required.");
        }

        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.OpenProject(session, request.ProjectPath!),
            requiresConfirm: true);
    }

    private static WorkerResponse CreateProject(WorkerRequest request)
    {
        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.CreateProject(
                session,
                request.ProjectDirectory!,
                request.ProjectName!,
                request.Author,
                request.Comment),
            requiresConfirm: true);
    }

    private static WorkerResponse SaveProject(WorkerRequest request)
    {
        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.SaveProject(session, request.ProjectPath),
            requiresConfirm: true);
    }

    private static WorkerResponse SaveProjectAs(WorkerRequest request)
    {
        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.SaveProjectAs(
                session,
                request.ProjectPath,
                request.TargetDirectory!,
                request.TargetName!,
                request.Rebind),
            requiresConfirm: true);
    }

    private static WorkerResponse ArchiveProject(WorkerRequest request)
    {
        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.ArchiveProject(
                session,
                request.ProjectPath,
                request.ArchiveDirectory!,
                request.ArchiveName!,
                request.ArchiveMode ?? ArchiveModeNames.Compressed,
                request.SaveBeforeArchive),
            requiresConfirm: true);
    }

    private static WorkerResponse CloseProject(WorkerRequest request)
    {
        return ProjectLifecycle(
            request,
            session => ProjectLifecycleService.CloseProject(session, request.ProjectPath, request.SaveBeforeClose),
            requiresConfirm: true);
    }

    private static WorkerResponse ProjectLifecycle(
        WorkerRequest request,
        Func<WorkerTiaPortalSession, ProjectLifecycleResultInfo> operation,
        bool requiresConfirm)
    {
        if (requiresConfirm && !request.Confirm)
        {
            return Failure("Operation not confirmed. Set confirm=true to proceed with the project operation.");
        }

        try
        {
            using var session = new WorkerTiaPortalSession(request.AllowTiaConfirmations, request.TiaVersion);
            var result = operation(session);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(result, JsonOptions)
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

    private static WorkerResponse ReadBlockInterface(WorkerRequest request)
    {
        if (string.IsNullOrEmpty(request.BlockPath))
            return Failure("BlockPath is required.");

        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var info = BlockInterfaceReader.Read(session.Project, request.BlockPath!);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(info, JsonOptions)
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

    private static WorkerResponse ExportPlcType(WorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TypeName))
            return Failure("TypeName is required.");

        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            string xml = PlcTypeExporter.Export(session.Project, request.TypeName!, request.PlcName, request.FolderPath);
            return new WorkerResponse { Success = true, Payload = xml };
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

    private static WorkerResponse ExportTagTableXml(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            string xml = TagTableExporter.Export(session.Project, request.TableName, request.PlcName, request.FolderPath);
            return new WorkerResponse { Success = true, Payload = xml };
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

    private static WorkerResponse ListConnections(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var connections = ConnectionReader.Read(session.Project);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(connections, JsonOptions)
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

    private static WorkerResponse BrowseHmiScreens(WorkerRequest request)
    {
        try
        {
            using var session = new WorkerTiaPortalSession(tiaVersion: request.TiaVersion);

            session.EnsureConnected();

            if (!string.IsNullOrEmpty(request.ProjectPath))
            {
                session.OpenProject(request.ProjectPath!);
            }

            if (session.Project is null)
            {
                return Failure("No project is open. Provide a projectPath argument or open a project in TIA Portal.");
            }

            var screens = HmiScreenReader.Read(session.Project, request.DeviceName);
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(screens, JsonOptions)
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

    private static WorkerResponse GetTiaVersion()
    {
        try
        {
            var active = AssemblyResolver.DetectedVersion;
            var summary = $"Connected to TIA Portal {active?.DisplayName ?? "Unknown"}. Active version: {active?.MajorVersion}.";
            return new WorkerResponse
            {
                Success = true,
                Payload = JsonSerializer.Serialize(new
                {
                    summary,
                    activeVersion = active?.MajorVersion,
                    activeDisplayName = active?.DisplayName
                }, JsonOptions)
            };
        }
        catch (Exception ex)
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
