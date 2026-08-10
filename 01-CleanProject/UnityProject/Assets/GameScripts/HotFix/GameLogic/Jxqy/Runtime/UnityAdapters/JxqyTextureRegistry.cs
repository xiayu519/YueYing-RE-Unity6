using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyTextureRegistry : IDisposable
    {
        private readonly Dictionary<string, Entry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        public void Register(
            string address,
            Texture2D texture,
            IDisposable lease = null)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "Texture address is empty.",
                    nameof(address));
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));
            if (_entries.TryGetValue(address, out Entry previous))
                previous.Lease?.Dispose();
            _entries[address] = new Entry(texture, lease);
        }

        public bool TryGet(string address, out Texture2D texture)
        {
            if (_entries.TryGetValue(address, out Entry entry))
            {
                texture = entry.Texture;
                return texture != null;
            }
            texture = null;
            return false;
        }

        public void Unregister(string address)
        {
            if (!_entries.TryGetValue(address, out Entry entry))
                return;
            _entries.Remove(address);
            entry.Lease?.Dispose();
        }

        public void Dispose()
        {
            foreach (Entry entry in _entries.Values)
                entry.Lease?.Dispose();
            _entries.Clear();
        }

        private readonly struct Entry
        {
            public Entry(Texture2D texture, IDisposable lease)
            {
                Texture = texture;
                Lease = lease;
            }

            public Texture2D Texture { get; }
            public IDisposable Lease { get; }
        }
    }
}
