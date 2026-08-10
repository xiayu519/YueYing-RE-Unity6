using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    public enum JxqyResourceDomain
    {
        Core,
        Shared,
        Scene,
        Mod,
    }

    /// <summary>
    /// Stable scene identity used as a resource namespace. It is independent
    /// from the generated Unity scene address and display name.
    /// </summary>
    public readonly struct JxqySceneKey : IEquatable<JxqySceneKey>
    {
        public JxqySceneKey(string value)
        {
            Value = NormalizePart(value, nameof(value));
        }

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(JxqySceneKey other)
        {
            return string.Equals(
                Value,
                other.Value,
                StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is JxqySceneKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(
            JxqySceneKey left,
            JxqySceneKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            JxqySceneKey left,
            JxqySceneKey right)
        {
            return !left.Equals(right);
        }

        internal static string NormalizePart(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Resource key part is empty.",
                    parameterName);
            string normalized = value.Trim()
                .Replace('\\', '/')
                .ToLowerInvariant();
            if (normalized.IndexOf('|') >= 0)
                throw new ArgumentException(
                    "Resource key parts cannot contain '|'.",
                    parameterName);
            return normalized;
        }
    }

    /// <summary>
    /// Logical resource identity. Gameplay code uses this key while manifests
    /// map it to a YooAsset package and physical address.
    /// </summary>
    public readonly struct JxqyResourceKey :
        IEquatable<JxqyResourceKey>
    {
        public JxqyResourceKey(
            JxqyResourceDomain domain,
            string owner,
            string kind,
            string localId)
        {
            Domain = domain;
            Owner = JxqySceneKey.NormalizePart(
                owner,
                nameof(owner));
            Kind = JxqySceneKey.NormalizePart(
                kind,
                nameof(kind));
            LocalId = JxqySceneKey.NormalizePart(
                localId,
                nameof(localId));
        }

        public JxqyResourceDomain Domain { get; }
        public string Owner { get; }
        public string Kind { get; }
        public string LocalId { get; }

        public static JxqyResourceKey Scene(
            JxqySceneKey sceneKey,
            string kind,
            string localId)
        {
            if (sceneKey.IsEmpty)
                throw new ArgumentException(
                    "Scene key is empty.",
                    nameof(sceneKey));
            return new JxqyResourceKey(
                JxqyResourceDomain.Scene,
                sceneKey.Value,
                kind,
                localId);
        }

        public static JxqyResourceKey Shared(
            string owner,
            string kind,
            string localId)
        {
            return new JxqyResourceKey(
                JxqyResourceDomain.Shared,
                owner,
                kind,
                localId);
        }

        public bool Equals(JxqyResourceKey other)
        {
            return Domain == other.Domain &&
                   string.Equals(
                       Owner,
                       other.Owner,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Kind,
                       other.Kind,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       LocalId,
                       other.LocalId,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is JxqyResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Domain;
                hash = hash * 397 ^
                       (Owner == null
                           ? 0
                           : StringComparer.Ordinal.GetHashCode(Owner));
                hash = hash * 397 ^
                       (Kind == null
                           ? 0
                           : StringComparer.Ordinal.GetHashCode(Kind));
                hash = hash * 397 ^
                       (LocalId == null
                           ? 0
                           : StringComparer.Ordinal.GetHashCode(LocalId));
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"{Domain.ToString().ToLowerInvariant()}|" +
                $"{Owner}|{Kind}|{LocalId}";
        }

        public static bool operator ==(
            JxqyResourceKey left,
            JxqyResourceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            JxqyResourceKey left,
            JxqyResourceKey right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public sealed class JxqyPreloadManifest
    {
        public int SchemaVersion = 2;
        public string GeneratorVersion = string.Empty;
        public string GeneratedUtc = string.Empty;
        public int MapGroupCount;
        public int GroupCount;
        public int ResourceEntryCount;
        public long ReferencedFileBytes;
        public List<JxqyPreloadGroup> Groups = new();
        public List<string> Errors = new();
        public List<string> IntentionalExclusions = new();
    }

    [Serializable]
    public sealed class JxqyPreloadGroup
    {
        public string Id = string.Empty;
        public string Kind = string.Empty;
        public string ResourceNamespace = string.Empty;
        public string SceneKey = string.Empty;
        public string OwnerStableId = string.Empty;
        public string OwnerRelativePath = string.Empty;
        public int ResourceCount;
        public long ReferencedFileBytes;
        public List<JxqyPreloadResource> Resources = new();
    }

    [Serializable]
    public sealed class JxqyPreloadResource
    {
        public string Address = string.Empty;
        public string LogicalKey = string.Empty;
        public string PackageName = string.Empty;
        public string ResourceKind = string.Empty;
        public string SourceStableId = string.Empty;
        public long FileBytes;
    }
}
