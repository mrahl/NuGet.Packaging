﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A portable implementation of the .NET FrameworkName type with added support for NuGet folder names.
    /// </summary>
    public partial class NuGetFramework : IEquatable<NuGetFramework>
    {
        private readonly string _frameworkIdentifier;
        private readonly Version _frameworkVersion;
        private readonly string _frameworkProfile;
        private const string _portable = "portable";
        private readonly string _runtimeIdentifier;

        public NuGetFramework(string framework)
            : this(framework, FrameworkConstants.EmptyVersion)
        {

        }

        public NuGetFramework(string framework, Version version)
            : this(framework, version, null)
        {

        }

        public NuGetFramework(string framework, Version version, string profile)
            : this(framework, version, profile, null)
        {

        }

        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string frameworkProfile, string runtimeIdentifier)
        {
            if (frameworkIdentifier == null)
            {
                throw new ArgumentNullException("frameworkIdentifier");
            }

            if (frameworkVersion == null)
            {
                throw new ArgumentNullException("frameworkVersion");
            }

            _frameworkIdentifier = frameworkIdentifier;
            _frameworkVersion = NormalizeVersion(frameworkVersion);
            _frameworkProfile = frameworkProfile ?? string.Empty;
            _runtimeIdentifier = runtimeIdentifier ?? string.Empty;
        }

        /// <summary>
        /// Target framework
        /// </summary>
        public string Framework
        {
            get
            {
                return _frameworkIdentifier;
            }
        }

        /// <summary>
        /// Target framework version
        /// </summary>
        public Version Version
        {
            get
            {
                return _frameworkVersion;
            }
        }

        /// <summary>
        /// True if the profile is non-empty
        /// </summary>
        public bool HasProfile
        {
            get
            {
                return !String.IsNullOrEmpty(Profile);
            }
        }

        /// <summary>
        /// True if the runtime identifier is non-empty
        /// </summary>
        public bool HasRuntime
        {
            get
            {
                return !String.IsNullOrEmpty(RuntimeIdentifier);
            }
        }

        /// <summary>
        /// Target framework profile
        /// </summary>
        public string Profile
        {
            get
            {
                return _frameworkProfile;
            }
        }

        /// <summary>
        /// Identifies the runtime (if any) that is targetted by this framework. For example: win81-x86
        /// </summary>
        public string RuntimeIdentifier
        {
            get
            {
                return _runtimeIdentifier;
            }
        }

        /// <summary>
        /// Similar in format to the .NET FrameworkName type
        /// </summary>
        /// <remarks>FrameworkName does not exist in Portable, otherwise this method would return it.</remarks>
        public string DotNetFrameworkName
        {
            get
            {
                string result = string.Empty;

                if (IsSpecificFramework)
                {
                    List<string> parts = new List<string>(3) { Framework };

                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Version=v{0}", GetDisplayVersion(Version)));

                    if (!String.IsNullOrEmpty(Profile))
                    {
                        parts.Add(String.Format(CultureInfo.InvariantCulture, "Profile={0}", Profile));
                    }

                    result = String.Join(", ", parts);
                }
                else
                {
                    result = String.Format(CultureInfo.InvariantCulture, "{0}, Version=v0.0", Framework);
                }

                return result;
            }
        }

        /// <summary>
        /// Creates the shortened version of the framework using the default mappings.
        /// Ex: net45
        /// </summary>
        public string GetShortFolderName()
        {
            return GetShortFolderName(DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates the shortened version of the framework using the given mappings.
        /// </summary>
        public string GetShortFolderName(IFrameworkNameProvider mappings)
        {
            StringBuilder sb = new StringBuilder();

            if (IsSpecificFramework)
            {
                string shortFramework = string.Empty;

                // get the framework
                if (!mappings.TryGetShortIdentifier(Framework, out shortFramework))
                {
                    shortFramework = GetLettersAndDigitsOnly(Framework);
                }

                if (String.IsNullOrEmpty(shortFramework))
                {
                    throw new FrameworkException("Invalid framework identifier");
                }

                // add framework
                sb.Append(shortFramework);

                // add the version if it is non-empty
                if (!AllFrameworkVersions)
                {
                    sb.Append(mappings.GetVersionString(Version));
                }

                if (IsPCL)
                {
                    sb.Append("-");

                    IEnumerable<NuGetFramework> frameworks = null;
                    if (HasProfile && mappings.TryGetPortableFrameworks(Profile, false, out frameworks) && frameworks.Any())
                    {
                        HashSet<NuGetFramework> required = new HashSet<NuGetFramework>(frameworks, NuGetFramework.Comparer);

                        // Normalize by removing all optional frameworks
                        mappings.TryGetPortableFrameworks(Profile, false, out frameworks);

                        // TODO: is there a scenario where optional frameworks are still needed in the string?
                        // mappings.TryGetPortableFrameworks(Profile, true, out frameworks);
                        // HashSet<NuGetFramework> optional = new HashSet<NuGetFramework>(frameworks.Where(e => !required.Contains(e)), NuGetFramework.Comparer);

                        // sort the PCL frameworks by alphabetical order
                        List<string> sortedFrameworks = required.Select(e => e.GetShortFolderName(mappings)).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();

                        // add optional frameworks at the end
                        // sortedFrameworks.AddRange(optional.Select(e => e.GetShortFolderName(mappings)).OrderBy(e => e, StringComparer.OrdinalIgnoreCase));

                        sb.Append(String.Join("+", sortedFrameworks));
                    }
                    else
                    {
                        throw new FrameworkException("Invalid portable frameworks");
                    }
                }
                else
                {
                    // add the profile
                    string shortProfile = string.Empty;

                    if (HasProfile && !mappings.TryGetShortProfile(Framework, Profile, out shortProfile))
                    {
                        // if we have a profile, but can't get a mapping, just use the profile as is
                        shortProfile = Profile;
                    }

                    if (!String.IsNullOrEmpty(shortProfile))
                    {
                        sb.Append("-");
                        sb.Append(shortProfile);
                    }
                }

                // add the runtime
                if(HasRuntime)
                {
                    sb.Append("~");
                    sb.Append(RuntimeIdentifier);
                }
            }
            else
            {
                // unsupported, any, agnostic
                sb.Append(Framework);
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static string GetDisplayVersion(Version version)
        {
            StringBuilder sb = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));

            if (version.Build > 0 || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return sb.ToString();
        }

        private static string GetLettersAndDigitsOnly(string s)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var c in s.ToCharArray())
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Portable class library check
        /// </summary>
        public bool IsPCL
        {
            get
            {
                return StringComparer.OrdinalIgnoreCase.Equals(Framework, FrameworkConstants.FrameworkIdentifiers.Portable) && Version.Major < 5;
            }
        }

        /// <summary>
        /// Runtime agnostic check
        /// </summary>
        public bool AnyRuntime
        {
            get
            {
                return String.IsNullOrEmpty(RuntimeIdentifier);
            }
        }

        /// <summary>
        /// True if this framework matches for all versions. 
        /// Ex: net
        /// </summary>
        public bool AllFrameworkVersions
        {
            get
            {
                return Version.Major == 0 && Version.Minor == 0 && Version.Build == 0 && Version.Revision == 0;
            }
        }

        /// <summary>
        /// True if this framework was invalid or unknown. This framework is only compatible with Any and Agnostic.
        /// </summary>
        public bool IsUnsupported
        {
            get
            {
                return UnsupportedFramework.Equals(this);
            }
        }

        /// <summary>
        /// True if this framework is non-specific. Always compatible.
        /// </summary>
        public bool IsAgnostic
        {
            get
            {
                return AgnosticFramework.Equals(this);
            }
        }

        /// <summary>
        /// True if this is the any framework. Always compatible.
        /// </summary>
        public bool IsAny
        {
            get
            {
                return AnyFramework.Equals(this);
            }
        }

        /// <summary>
        /// True if this framework is real and not one of the special identifiers.
        /// </summary>
        public bool IsSpecificFramework
        {
            get
            {
                return !IsAgnostic && !IsAny && !IsUnsupported;
            }
        }

        /// <summary>
        /// Full framework comparison of the identifier, version, profile, platform, and platform version
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> Comparer = new NuGetFrameworkFullComparer();

        /// <summary>
        /// Framework name only comparison.
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> FrameworkNameComparer = new NuGetFrameworkNameComparer();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(DotNetFrameworkName);

            if (!String.IsNullOrEmpty(RuntimeIdentifier))
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", Runtime={0}", RuntimeIdentifier));
            }

            return sb.ToString();
        }

        public bool Equals(NuGetFramework other)
        {
            return Comparer.Equals(this, other);
        }

        public override int GetHashCode()
        {
            return Comparer.GetHashCode(this);
        }

        public override bool Equals(object obj)
        {
            NuGetFramework other = obj as NuGetFramework;

            if (other != null)
            {
                return Equals(other);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        private static Version NormalizeVersion(Version version)
        {
            Version normalized = version;

            if (version.Build < 0 || version.Revision < 0)
            {
                normalized = new Version(
                               version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
            }

            return normalized;
        }
    }
}
