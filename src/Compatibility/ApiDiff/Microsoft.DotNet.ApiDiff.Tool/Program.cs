// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiDiff.Tool;

/// <summary>
/// Entrypoint for the genapidiff tool, which generates a markdown diff of two
/// different versions of the same assembly, using the specified command line options.
/// </summary>
public static class Program
{
    private static readonly string AttributesToExcludeDefaultFileName = "AttributesToExclude.txt";

    public static Task Main(string[] args) => Start(args);

    public static async Task Start(string[] args)
    {
        RootCommand rootCommand = new("ApiDiff - Tool for generating a markdown diff of two different versions of the same assembly.")
        {
            TreatUnmatchedTokensAsErrors = true
        };

        Option<string> optionBeforeAssembliesFolderPath = new(name: "BeforeAssemblies", aliases: ["--before", "-b"])
        {
            Description = "The path to the folder containing the old (before) assemblies to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            Required = true
        };

        Option<string> optionBeforeRefAssembliesFolderPath = new(name: "BeforeRefAssemblies", aliases: ["--refbefore", "-rb"])
        {
            Description = "The path to the folder containing the references required by old (before) assemblies, not to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            Required = false
        };

        Option<string> optionAfterAssembliesFolderPath = new(name: "AfterAssemblies", aliases: ["--after", "-a"])
        {
            Description = "The path to the folder containing the new (after) assemblies to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            Required = true
        };

        Option<string> optionAfterRefAssembliesFolderPath = new(name: "AfterRefAssemblies", aliases: ["--refafter", "-ra"])
        {
            Description = "The path to the folder containing references required by the new (after) reference assemblies, not to be included in the diff.",
            Arity = ArgumentArity.ExactlyOne,
            Required = false
        };

        Option<string> optionOutputFolderPath = new(name: "OutputFolder", aliases: ["--output", "-o"])
        {
            Description = "The path to the output folder.",
            Arity = ArgumentArity.ExactlyOne,
            Required = true
        };

        Option<string> optionBeforeFriendlyName = new(name: "BeforeFriendlyName", aliases: ["--beforeFriendlyName", "-bfn"])
        {
            Description = "The friendly name to describe the 'before' assembly.",
            Arity = ArgumentArity.ExactlyOne,
            Required = true
        };

        Option<string> optionAfterFriendlyName = new(name: "AfterFriendlyName", aliases: ["--afterFriendlyName", "-afn"])
        {
            Description = "The friendly name to describe the 'after' assembly.",
            Arity = ArgumentArity.ExactlyOne,
            Required = true
        };

        Option<string> optionTableOfContentsTitle = new(name: "TableOfContents", aliases: ["--tableOfContentsTitle", "-tc"])
        {
            Description = $"The optional title of the markdown table of contents file that is placed in the output folder.",
            Arity = ArgumentArity.ExactlyOne,
            Required = false,
            DefaultValueFactory = _ => "api_diff"
        };

        Option<FileInfo[]?> optionFilesWithAssembliesToExclude = new(name: "FilesWithAssembliesToExclude", aliases: ["--assembliesToExclude", "-eas"])
        {
            Description = "An optional array of filepaths, each containing a list of assemblies that should be excluded from the diff. Each file should contain one assembly name per line, with no extensions.",
            Arity = ArgumentArity.ZeroOrMore,
            Required = false,
            DefaultValueFactory = _ => null
        };

        Option<FileInfo[]?> optionFilesWithAttributesToExclude = new(name: "FilesWithAttributesToExclude", aliases: ["--attributesToExclude", "-eattrs"])
        {
            Description = $"An optional array of filepaths, each containing a list of attributes to exclude from the diff. Each file should contain one API full name per line. You can either modify the default file '{AttributesToExcludeDefaultFileName}' to add your own attributes, or include additional files using this command line option.",
            Arity = ArgumentArity.ZeroOrMore,
            Required = false,
            DefaultValueFactory = _ => [new FileInfo(Path.Join(AppContext.BaseDirectory, AttributesToExcludeDefaultFileName))]
        };

        Option<FileInfo[]?> optionFilesWithApisToExclude = new(name: "FilesWithApisToExclude", aliases: ["--apisToExclude", "-eapis"])
        {
            Description = "An optional array of filepaths, each containing a list of APIs to exclude from the diff. Each file should contain one API full name per line.",
            Arity = ArgumentArity.ZeroOrMore,
            Required = false,
            DefaultValueFactory = _ => null
        };

        Option<bool> optionAddPartialModifier = new(name: "AddPartialModifier", aliases: ["--addPartialModifier", "-apm"])
        {
            Description = "Add the 'partial' modifier to types.",
            DefaultValueFactory = _ => false
        };

        Option<bool> optionAttachDebugger = new(name: "AttachDebugger", aliases: ["--attachDebugger", "-d"])
        {
            Description = "Stops the tool at startup, prints the process ID and waits for a debugger to attach.",
            DefaultValueFactory = _ => false
        };

        // Custom ordering for the help menu.
        rootCommand.Options.Add(optionBeforeAssembliesFolderPath);
        rootCommand.Options.Add(optionBeforeRefAssembliesFolderPath);
        rootCommand.Options.Add(optionAfterAssembliesFolderPath);
        rootCommand.Options.Add(optionAfterRefAssembliesFolderPath);
        rootCommand.Options.Add(optionOutputFolderPath);
        rootCommand.Options.Add(optionBeforeFriendlyName);
        rootCommand.Options.Add(optionAfterFriendlyName);
        rootCommand.Options.Add(optionTableOfContentsTitle);
        rootCommand.Options.Add(optionFilesWithAssembliesToExclude);
        rootCommand.Options.Add(optionFilesWithAttributesToExclude);
        rootCommand.Options.Add(optionFilesWithApisToExclude);
        rootCommand.Options.Add(optionAddPartialModifier);
        rootCommand.Options.Add(optionAttachDebugger);

        rootCommand.SetAction(async (ParseResult result) =>
        {
            try
            {
                DiffConfiguration c = new(
                    beforeAssembliesFolderPath: result.GetValue(optionBeforeAssembliesFolderPath) ?? throw new NullReferenceException("Null before assemblies directory"),
                    beforeAssemblyReferencesFolderPath: result.GetValue(optionBeforeRefAssembliesFolderPath),
                    afterAssembliesFolderPath: result.GetValue(optionAfterAssembliesFolderPath) ?? throw new NullReferenceException("Null after assemblies directory"),
                    afterAssemblyReferencesFolderPath: result.GetValue(optionAfterRefAssembliesFolderPath),
                    outputFolderPath: result.GetValue(optionOutputFolderPath) ?? throw new NullReferenceException("Null output directory"),
                    beforeFriendlyName: result.GetValue(optionBeforeFriendlyName) ?? throw new NullReferenceException("Null before friendly name"),
                    afterFriendlyName: result.GetValue(optionAfterFriendlyName) ?? throw new NullReferenceException("Null after friendly name"),
                    tableOfContentsTitle: result.GetValue(optionTableOfContentsTitle) ?? throw new NullReferenceException("Null table of contents title"),
                    filesWithAssembliesToExclude: result.GetValue(optionFilesWithAssembliesToExclude),
                    filesWithAttributesToExclude: result.GetValue(optionFilesWithAttributesToExclude),
                    filesWithApisToExclude: result.GetValue(optionFilesWithApisToExclude),
                    addPartialModifier: result.GetValue(optionAddPartialModifier),
                    attachDebugger: result.GetValue(optionAttachDebugger)
                );

                if (c.AttachDebugger)
                {
                    WaitForDebugger();
                }

                await HandleCommandAsync(c).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Unhandled exception: {e}");
                throw;
            }
        });
        await rootCommand.Parse(args).InvokeAsync();
    }

    private static Task HandleCommandAsync(DiffConfiguration diffConfig)
    {
        var log = new ConsoleLog(MessageImportance.Normal);

        string assembliesToExclude = string.Join(", ", diffConfig.FilesWithAssembliesToExclude?.Select(a => a.FullName) ?? []);
        string attributesToExclude = string.Join(", ", diffConfig.FilesWithAttributesToExclude?.Select(a => a.FullName) ?? []);
        string apisToExclude = string.Join(", ", diffConfig.FilesWithApisToExclude?.Select(a => a.FullName) ?? []);

        // Custom ordering to match help menu.
        log.LogMessage("Selected options:");
        log.LogMessage($" - 'Before' source assemblies:         {diffConfig.BeforeAssembliesFolderPath}");
        log.LogMessage($" - 'After'  source assemblies:         {diffConfig.AfterAssembliesFolderPath}");
        log.LogMessage($" - 'Before' reference assemblies:      {diffConfig.BeforeAssemblyReferencesFolderPath}");
        log.LogMessage($" - 'After'  reference assemblies:      {diffConfig.AfterAssemblyReferencesFolderPath}");
        log.LogMessage($" - Output:                             {diffConfig.OutputFolderPath}");
        log.LogMessage($" - Files with assemblies to exclude:   {assembliesToExclude}");
        log.LogMessage($" - Files with attributes to exclude:   {attributesToExclude}");
        log.LogMessage($" - Files with APIs to exclude:         {apisToExclude}");
        log.LogMessage($" - 'Before' friendly name:             {diffConfig.BeforeFriendlyName}");
        log.LogMessage($" - 'After' friendly name:              {diffConfig.AfterFriendlyName}");
        log.LogMessage($" - Table of contents title:            {diffConfig.TableOfContentsTitle}");
        log.LogMessage($" - Add partial modifier to types:      {diffConfig.AddPartialModifier}");
        log.LogMessage($" - Attach debugger:                    {diffConfig.AttachDebugger}");
        log.LogMessage("");

        IDiffGenerator diffGenerator = DiffGeneratorFactory.Create(log,
                                                                   diffConfig.BeforeAssembliesFolderPath,
                                                                   diffConfig.BeforeAssemblyReferencesFolderPath,
                                                                   diffConfig.AfterAssembliesFolderPath,
                                                                   diffConfig.AfterAssemblyReferencesFolderPath,
                                                                   diffConfig.OutputFolderPath,
                                                                   diffConfig.BeforeFriendlyName,
                                                                   diffConfig.AfterFriendlyName,
                                                                   diffConfig.TableOfContentsTitle,
                                                                   diffConfig.FilesWithAssembliesToExclude,
                                                                   diffConfig.FilesWithAttributesToExclude,
                                                                   diffConfig.FilesWithApisToExclude,
                                                                   diffConfig.AddPartialModifier,
                                                                   writeToDisk: true,
                                                                   diagnosticOptions: null // TODO: If needed, add CLI option to pass specific diagnostic options
                                                                   );

        return diffGenerator.RunAsync();
    }

    private static void WaitForDebugger()
    {
        while (!Debugger.IsAttached)
        {
            Console.WriteLine($"Attach to process {Environment.ProcessId}...");
            Thread.Sleep(1000);
        }
        Console.WriteLine("Debugger attached!");
        Debugger.Break();
    }
}
