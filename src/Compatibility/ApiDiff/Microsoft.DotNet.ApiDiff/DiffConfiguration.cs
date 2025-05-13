// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Defines the necessary configuration options for API diff.
/// </summary>
public class DiffConfiguration
{
    public DiffConfiguration(
        string afterAssembliesFolderPath,
        string? afterAssemblyReferencesFolderPath,
        string beforeAssembliesFolderPath,
        string? beforeAssemblyReferencesFolderPath,
        string outputFolderPath,
        string beforeFriendlyName,
        string afterFriendlyName,
        string tableOfContentsTitle,
        FileInfo[]? filesWithAssembliesToExclude,
        FileInfo[]? filesWithAttributesToExclude,
        FileInfo[]? filesWithApisToExclude,
        bool addPartialModifier,
        bool attachDebugger
    )
    {
        ValidateMandatoryDirectoryExists(afterAssembliesFolderPath);
        ValidateMandatoryDirectoryExists(beforeAssembliesFolderPath);
        ValidateMandatoryDirectoryExists(outputFolderPath);
        ValidateFilesExist(filesWithAssembliesToExclude);
        ValidateFilesExist(filesWithAttributesToExclude);
        ValidateFilesExist(filesWithApisToExclude);
        ValidateOptionalDirectoryExists(afterAssemblyReferencesFolderPath);
        ValidateOptionalDirectoryExists(beforeAssemblyReferencesFolderPath);

        AfterAssembliesFolderPath = afterAssembliesFolderPath;
        AfterAssemblyReferencesFolderPath = afterAssemblyReferencesFolderPath;
        BeforeAssembliesFolderPath = beforeAssembliesFolderPath;
        BeforeAssemblyReferencesFolderPath = beforeAssemblyReferencesFolderPath;
        OutputFolderPath = outputFolderPath;
        BeforeFriendlyName = beforeFriendlyName;
        AfterFriendlyName = afterFriendlyName;
        TableOfContentsTitle = tableOfContentsTitle;
        FilesWithAssembliesToExclude = filesWithAssembliesToExclude;
        FilesWithAttributesToExclude = filesWithAttributesToExclude;
        FilesWithApisToExclude = filesWithApisToExclude;
        AddPartialModifier = addPartialModifier;
        AttachDebugger = attachDebugger;
    }

    private static void ValidateMandatoryDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
        }
    }

    private static void ValidateOptionalDirectoryExists(string? path)
    {
        if (path is not null)
        {
            ValidateMandatoryDirectoryExists(path);
        }
    }

    public string AfterAssembliesFolderPath { get; set; }
    public string? AfterAssemblyReferencesFolderPath { get; set; }
    public string BeforeAssembliesFolderPath { get; set; }
    public string? BeforeAssemblyReferencesFolderPath { get; set; }
    public string OutputFolderPath { get; set; }
    public string BeforeFriendlyName { get; set; }
    public string AfterFriendlyName { get; set; }
    public string TableOfContentsTitle { get; set; }
    public FileInfo[]? FilesWithAssembliesToExclude { get; set; }
    public FileInfo[]? FilesWithAttributesToExclude { get; set; }
    public FileInfo[]? FilesWithApisToExclude { get; set; }
    public bool AddPartialModifier { get; set; }
    public bool AttachDebugger { get; set; }


    private static void ValidateFilesExist(FileInfo[]? files)
    {
        if (files is null || files.Length == 0)
        {
            return;
        }

        foreach (FileInfo file in files)
        {
            if (!file.Exists)
            {
                throw new FileNotFoundException($"The file '{file.FullName}' does not exist.");
            }
        }
    }
}
