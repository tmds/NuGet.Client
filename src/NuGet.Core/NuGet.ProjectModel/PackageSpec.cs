// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents the specification of a package that can be built.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class PackageSpec
    {
        public static readonly string PackageSpecFileName = "project.json";

        public PackageSpec(IList<TargetFrameworkInformation> frameworks)
            : this(new JObject())
        {
            TargetFrameworks = frameworks;
            Properties = new JObject();
        }

        public PackageSpec(JObject rawProperties)
        {
            TargetFrameworks = new List<TargetFrameworkInformation>();
            Properties = rawProperties;
        }

        public string FilePath { get; set; }

        public string BaseDirectory
        {
            get { return Path.GetDirectoryName(FilePath); }
        }

        public string Name { get; set; }

        public string Title { get; set; }

        private NuGetVersion _version;
        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
                this.IsDefaultVersion = false;
            }
        }
        public bool IsDefaultVersion { get; set; }

        public bool HasVersionSnapshot { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string ReleaseNotes { get; set; }

        public string[] Authors { get; set; } = new string[0];

        public string[] Owners { get; set; } = new string[0];

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public BuildOptions BuildOptions { get; set; }

        public string[] Tags { get; set; } = new string[0];

        public IList<string> ContentFiles { get; set; } = new List<string>();

        public IList<LibraryDependency> Dependencies { get; set; } = new List<LibraryDependency>();

        public IList<ToolDependency> Tools { get; set; } = new List<ToolDependency>();

        public IDictionary<string, IEnumerable<string>> Scripts { get; private set; } = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, string> PackInclude { get; private set; } = new Dictionary<string, string>();

        public PackOptions PackOptions { get; set; } = new PackOptions();

        public IList<TargetFrameworkInformation> TargetFrameworks { get; private set; } = new List<TargetFrameworkInformation>();

        public RuntimeGraph RuntimeGraph { get; set; } = new RuntimeGraph();

        /// <summary>
        /// Additional MSBuild properties.
        /// </summary>
        /// <remarks>Optional. This is normally set for internal use only.</remarks>
        public ProjectRestoreMetadata RestoreMetadata { get; set; }

        /// <summary>
        /// Gets a list of all properties found in the package spec, including
        /// those not recognized by the parser.
        /// </summary>
        public JObject Properties { get; }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();
            
            hashCode.AddObject(Title);
            hashCode.AddObject(Version);
            hashCode.AddObject(IsDefaultVersion);
            hashCode.AddObject(HasVersionSnapshot);
            hashCode.AddObject(Description);
            hashCode.AddObject(Summary);
            hashCode.AddObject(ReleaseNotes);
            hashCode.AddSequence(Authors);
            hashCode.AddSequence(Owners);
            hashCode.AddObject(ProjectUrl);
            hashCode.AddObject(IconUrl);
            hashCode.AddObject(LicenseUrl);
            hashCode.AddObject(RequireLicenseAcceptance);
            hashCode.AddObject(Copyright);
            hashCode.AddObject(Language);
            hashCode.AddObject(BuildOptions);
            hashCode.AddSequence(Tags);
            hashCode.AddSequence(ContentFiles);
            hashCode.AddSequence(Dependencies);
            hashCode.AddSequence(Tools);
            hashCode.AddDictionary(Scripts);
            hashCode.AddDictionary(PackInclude);
            hashCode.AddObject(PackOptions);
            hashCode.AddSequence(TargetFrameworks);
            hashCode.AddObject(RuntimeGraph);
            hashCode.AddObject(RestoreMetadata);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageSpec);
        }

        public bool Equals(PackageSpec other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // Name and FilePath are not used for comparison since they are not serialized to JSON.

            return Title == other.Title &&
                   EqualityUtility.EqualsWithNullCheck(Version, other.Version) &&
                   IsDefaultVersion == other.IsDefaultVersion &&
                   HasVersionSnapshot == other.HasVersionSnapshot &&
                   Description == other.Description &&
                   Summary == other.Summary &&
                   ReleaseNotes == other.ReleaseNotes &&
                   EqualityUtility.SequenceEqualWithNullCheck(Authors, other.Authors) &&
                   EqualityUtility.SequenceEqualWithNullCheck(Owners, other.Owners) &&
                   ProjectUrl == other.ProjectUrl &&
                   IconUrl == other.IconUrl &&
                   LicenseUrl == other.LicenseUrl &&
                   RequireLicenseAcceptance == other.RequireLicenseAcceptance &&
                   Copyright == other.Copyright &&
                   Language == other.Language &&
                   EqualityUtility.EqualsWithNullCheck(BuildOptions, other.BuildOptions) &&
                   EqualityUtility.SequenceEqualWithNullCheck(Tags, other.Tags) &&
                   EqualityUtility.SequenceEqualWithNullCheck(ContentFiles, other.ContentFiles) &&
                   EqualityUtility.SequenceEqualWithNullCheck(Dependencies, other.Dependencies) &&
                   EqualityUtility.SequenceEqualWithNullCheck(Tools, other.Tools) &&
                   EqualityUtility.DictionaryOfSequenceEquals(Scripts, other.Scripts) &&
                   EqualityUtility.DictionaryEquals(PackInclude, other.PackInclude, (s, o) => StringComparer.Ordinal.Equals(s, o)) &&
                   EqualityUtility.EqualsWithNullCheck(PackOptions, other.PackOptions) &&
                   EqualityUtility.SequenceEqualWithNullCheck(TargetFrameworks, other.TargetFrameworks) &&
                   EqualityUtility.EqualsWithNullCheck(RuntimeGraph, other.RuntimeGraph) &&
                   EqualityUtility.EqualsWithNullCheck(RestoreMetadata, other.RestoreMetadata);
        }

        /// <summary>
        /// Clone a PackageSpec and underlying JObject.
        /// </summary>
        public PackageSpec Clone()
        {
            var writer = new JsonObjectWriter();
            PackageSpecWriter.Write(this, writer);
            var json = writer.GetJObject();

            var spec = JsonPackageSpecReader.GetPackageSpec(json);
            spec.Name = Name;
            spec.FilePath = FilePath;

            return spec;
        }
    }
}
