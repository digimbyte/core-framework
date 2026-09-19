using System;
using UnityEngine;

namespace Core.Enums
{
    /// <summary>
    /// Serializable string-backed enum-like value.
    /// In generated code you will use it like: SerialEnum ammoKey = ammo.medium;
    /// and compare with: if (ammoKey == ammo.medium) { ... }
    /// </summary>
    [Serializable]
    public struct SerialEnum : IEquatable<SerialEnum>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string key;

        public SerialEnum(string key)
        {
            this.key = key?.ToLowerInvariant();
        }

        /// <summary>
        /// Underlying string key, e.g. "ammo.medium".
        /// </summary>
        public string Key => key?.ToLowerInvariant() ?? string.Empty;

        public void OnBeforeSerialize() => key = key?.ToLowerInvariant();
        public void OnAfterDeserialize() => key = key?.ToLowerInvariant();

        public override string ToString() => Key;

        // Equality and hashing ignore case.
        public bool Equals(SerialEnum other) => string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => obj is SerialEnum other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Key);

        public static bool operator ==(SerialEnum left, SerialEnum right) => left.Equals(right);
        public static bool operator !=(SerialEnum left, SerialEnum right) => !left.Equals(right);

        // Convenience implicit conversion to string when needed.
        public static implicit operator string(SerialEnum value) => value.Key;
    }
}
