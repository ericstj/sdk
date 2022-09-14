﻿using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public class CreateNewImage : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The base registry to pull from.
    /// Ex: https://mcr.microsoft.com
    /// </summary>
    [Required]
    public string BaseRegistry { get; set; }

    /// <summary>
    /// The base image to pull.
    /// Ex: dotnet/runtime
    /// </summary>
    [Required]
    public string BaseImageName { get; set; }

    /// <summary>
    /// The base image tag.
    /// Ex: 6.0
    /// </summary>
    [Required]
    public string BaseImageTag { get; set; }

    /// <summary>
    /// The registry to push to.
    /// </summary>
    [Required]
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The name of the output image that will be pushed to the registry.
    /// </summary>
    [Required]
    public string ImageName { get; set; }

    /// <summary>
    /// The tag to associate with the new image.
    /// </summary>
    public string[] ImageTags { get; set; }

    /// <summary>
    /// The directory for the build outputs to be published.
    /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
    /// </summary>
    [Required]
    public string PublishDirectory { get; set; }

    /// <summary>
    /// The working directory of the container.
    /// </summary>
    [Required]
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// The entrypoint application of the container.
    /// </summary>
    [Required]
    public ITaskItem[] Entrypoint { get; set; }

    /// <summary>
    /// Arguments to pass alongside Entrypoint.
    /// </summary>
    public ITaskItem[] EntrypointArgs { get; set; }

    /// <summary>
    /// Ports that the application declares that it will use.
    /// Note that this means nothing to container hosts, by default -
    /// it's mostly documentation.
    /// </summary>
    public ITaskItem[] ExposedPorts { get; set; }

    /// <summary>
    /// Labels that the image configuration will include in metadata
    /// </summary>
    public ITaskItem[] Labels { get; set; }

    private bool IsDockerPush { get => OutputRegistry == "docker://"; }

    public CreateNewImage()
    {
        BaseRegistry = "";
        BaseImageName = "";
        BaseImageTag = "";
        OutputRegistry = "";
        ImageName = "";
        ImageTags = Array.Empty<string>();
        PublishDirectory = "";
        WorkingDirectory = "";
        Entrypoint = Array.Empty<ITaskItem>();
        EntrypointArgs = Array.Empty<ITaskItem>();
        Labels = Array.Empty<ITaskItem>();
        ExposedPorts = Array.Empty<ITaskItem>();
    }

    private void SetPorts(Image image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portTy = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portTy, out var parsedPort, out var errors))
            {
                image.ExposePort(parsedPort.number, parsedPort.type);
            }
            else
            {
                ContainerHelpers.ParsePortError parsedErrors = (ContainerHelpers.ParsePortError)errors!;
                var portString = portTy == null ? portNo : $"{portNo}/{portTy}";
                if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.MissingPortNumber))
                {
                    Log.LogError("ContainerPort item '{0}' does not specify the port number. Please ensure the item's Include is a port number, for example '<ContainerPort Include=\"80\" />'", port.ItemSpec);
                }
                else
                {
                    var message = "A ContainerPort item was provided with ";
                    var arguments = new List<string>(2);
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) && parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}' and an invalid port type '{1}'";
                        arguments.Add(portNo);
                        arguments.Add(portTy!);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}'";
                        arguments.Add(portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port type '{0}'";
                        arguments.Add(portTy!);
                    }
                    message += ". ContainerPort items must have an Include value that is an integer, and a Type value that is either 'tcp' or 'udp'";

                    Log.LogError(message, arguments);
                }
            }
        }

    }

    public override bool Execute()
    {
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        Registry reg;
        Image image;

        try
        {
            reg = new Registry(new Uri(BaseRegistry, UriKind.RelativeOrAbsolute));
            image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
        }
        catch
        {
            throw;
        }

        if (BuildEngine != null)
        {
            Log.LogMessage($"Loading from directory: {PublishDirectory}");
        }

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
        image.AddLayer(newLayer);
        image.WorkingDirectory = WorkingDirectory;
        image.SetEntrypoint(Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (var label in Labels)
        {
            image.Label(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetPorts(image, ExposedPorts);

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        var isDockerPush = OutputRegistry.StartsWith("docker://");
        Registry? outputReg = isDockerPush ? null : new Registry(new Uri(OutputRegistry));
        foreach (var tag in ImageTags)
        {
            if (isDockerPush)
            {
                try
                {
                    LocalDocker.Load(image, ImageName, tag, BaseImageName).Wait();
                    if (BuildEngine != null)
                    {
                        Log.LogMessage(MessageImportance.High, "Pushed container '{0}:{1}' to Docker daemon", ImageName, tag);
                    }
                }
                catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
                {
                    Log.LogErrorFromException(dle, showStackTrace: false);
                }
            }
            else
            {
                try
                {
                    outputReg?.Push(image, ImageName, tag, BaseImageName).Wait();
                    if (BuildEngine != null)
                    {
                        Log.LogMessage(MessageImportance.High, "Pushed container '{0}:{1}' to registry '{2}'", ImageName, tag, OutputRegistry);
                    }
                }
                catch (Exception e)
                {
                    if (BuildEngine != null)
                    {
                        Log.LogError("Failed to push to the output registry: {0}", e);
                    }
                }
            }
        }

        return !Log.HasLoggedErrors;
    }
}