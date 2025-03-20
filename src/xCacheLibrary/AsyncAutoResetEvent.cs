// --------------------------------------------------------
// Copyright (c) Astra. All rights reserved.
// 
// Author:  Giovanny Hernandez
// Created: March 19, 2025
// --------------------------------------------------------

namespace xCacheLibrary;

public class AsyncAutoResetEvent
{
    private readonly SemaphoreSlim m_semaphore = new SemaphoreSlim(0, 1);

    /// <summary>
    /// This starts at 0, the thread waits. If multiple threads call WaitAsync(), they all wait in a queue.
    /// </summary>
    public async Task WaitAsync()
    {
        await m_semaphore.WaitAsync();
    }

    /// <summary>
    /// Only one waiting thread is allowed to proceed. The next thread stays blocked until Set() is called again.
    /// If no threads are waiting, the count stays at 1 until a thread consumes it.
    /// </summary>
    public void Set()
    {
        // Ensure we don't release more than one thread
        if (m_semaphore.CurrentCount == 0)
            m_semaphore.Release();
    }
}