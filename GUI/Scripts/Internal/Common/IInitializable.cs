
using System;

namespace Aura.Internal.Common
{
    internal interface IInitializable : IDisposable
    {
        void Init();
    }

    internal interface ICapacityInitializable : IDisposable
    {
        void Init(int capacity = 0);
    }
}
