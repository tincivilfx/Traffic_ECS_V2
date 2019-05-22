using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace CivilFX.TrafficECS
{

    public unsafe class CustomMemoryManagerBase
    {
        protected void* ptr;
        protected Allocator allocator;

        public CustomMemoryManagerBase()
        {
            ptr = null;
        }

        ~CustomMemoryManagerBase()
        {
            if (ptr != null)
            {
                Debug.Log("Deallocate");
                UnsafeUtility.Free(ptr, allocator);
            }
        }
    }

    public unsafe class CustomMemoryManager<T> : CustomMemoryManagerBase where T : unmanaged
    {
        public CustomMemoryManager() : base()
        {

        }

        public void AllocateMemory(long size, int alignment = 32, Allocator _allocator = Allocator.Persistent)
        {
            allocator = _allocator;
            try
            {
                ptr = UnsafeUtility.Malloc(size * UnsafeUtility.SizeOf<T>(), alignment, allocator);
                //Debug.Log("Allocate: " + size * UnsafeUtility.SizeOf<T>());
            }
            catch (Exception ex)
            {
                //Debug.LogError("Failed to allocate memory: " + ex.Message);
                throw ex;
            }
        }

        public T* GetPointer()
        {
            return ptr == null ? null : (T*)ptr;
        }
    }
}