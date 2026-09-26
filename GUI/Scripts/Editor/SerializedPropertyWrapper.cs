
using UnityEditor;

namespace Aura.Editor.Serialization
{
    internal interface ISerializedPropertyWrapper
    {
        SerializedProperty SerializedProperty { get; set; }
    }
}

